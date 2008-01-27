// mDCM: A C# DICOM library
//
// Copyright (c) 2006-2008  Colby Dillion
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// Author:
//    Colby Dillion (colby.dillion@gmail.com)

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Dicom.Codec;
using Dicom.IO;

namespace Dicom.Data {
	public class DcmDataset {
		#region Protected Members
		protected SortedList<uint, DcmItem> _items = new SortedList<uint, DcmItem>();

		protected long _streamPosition = 0;
		protected uint _streamLength = 0xffffffff;

		protected DcmTS _transferSyntax;
		#endregion

		#region Public Constructors
		//public DcmDataset() {
		//    _transferSyntax = DcmTS.ExplicitVRLittleEndian;
		//}
		public DcmDataset(DcmTS ts) {
			_transferSyntax = ts;
		}
		//public DcmDataset(long pos, uint len) {
		//    _streamPosition = pos;
		//    _streamLength = len;
		//    _transferSyntax = DcmTS.ExplicitVRLittleEndian;
		//}
		public DcmDataset(long pos, uint len, DcmTS ts) {
			_streamPosition = pos;
			_streamLength = len;
			_transferSyntax = ts;
		}
		#endregion

		#region Public Properties
		public long StreamPosition {
			get { return _streamPosition; }
		}
		public uint StreamLength {
			get { return _streamLength; }
		}
		public DcmTS InternalTransferSyntax {
			get { return _transferSyntax; }
		}

		public IList<DcmItem> Elements {
			get { return _items.Values; }
		}
		#endregion

		#region Internal Use Methods
		internal uint CalculateGroupWriteLength(ushort group, DcmTS syntax, DicomWriteOptions options) {
			uint length = 0;
			foreach (DcmItem item in _items.Values) {
				if (item.Tag.Group < group || item.Tag.Element == 0x0000)
					continue;
				if (item.Tag.Group > group)
					return length;
				length += item.CalculateWriteLength(syntax, options);
			}
			return length;
		}

		internal uint CalculateWriteLength(DcmTS syntax, DicomWriteOptions options) {
			uint length = 0;
			ushort group = 0xffff;
			foreach (DcmItem item in _items.Values) {
				if (item.Tag.Element == 0x0000)
					continue;
				if (item.Tag.Group != group) {
					group = item.Tag.Group;
					if (Flags.IsSet(options, DicomWriteOptions.CalculateGroupLengths)) {
						if (syntax.IsExplicitVR)
							length += 4 + 2 + 2 + 4;
						else
							length += 4 + 4 + 4;
					}
				}
				length += item.CalculateWriteLength(syntax, options);
			}
			return length;
		}

		internal void SelectByteOrder(Endian endian) {
			foreach (DcmItem item in _items.Values) {
				item.SelectByteOrder(endian);
			}
		}

		public void PreloadDeferredBuffers() {
			foreach (DcmItem item in _items.Values) {
				item.Preload();
			}
		}

		public void UnloadDeferredBuffers() {
			foreach (DcmItem item in _items.Values) {
				item.Unload();
			}
		}
		#endregion

		#region Element Access Methods
		public void Remove(DcmTag tag) {
			try { _items.Remove(tag.Card); }
			catch { }
		}

		public void Remove(DcmTagMask mask) {
			for (int i = 0; i < _items.Values.Count; i++) {
				if (mask.IsMatch(_items.Values[i].Tag)) {
					try { _items.RemoveAt(i--); }
					catch { }
				}
			}
		}

		public bool AddElement(DcmTag tag) {
			return AddElement(tag, tag.Entry.DefaultVR);
		}

		public bool AddElement(DcmTag tag, DcmVR vr) {
			DcmElement elem = DcmElement.Create(tag, vr);
			AddItem(elem);
			return true;
		}

		public bool AddElementWithValue(DcmTag tag, string value) {
			DcmVR vr = tag.Entry.DefaultVR;
			if (!vr.IsString)
				throw new DcmDataException("Tried to create element with incorrect VR");
			if (AddElement(tag, vr)) {
				if (value != null && value != String.Empty)
					SetString(tag, value);
				return true;
			}
			return false;
		}

		public bool AddElementWithValue(DcmTag tag, ushort value) {
			DcmVR vr = tag.Entry.DefaultVR;
			if (vr != DcmVR.US)
				throw new DcmDataException("Tried to create element with incorrect VR");
			if (AddElement(tag, vr)) {
				GetUS(tag).SetValue(value);
				return true;
			}
			return false;
		}

		public bool AddElementWithValue(DcmTag tag, int value) {
			DcmVR vr = tag.Entry.DefaultVR;
			if (vr != DcmVR.IS && vr != DcmVR.SL)
				throw new DcmDataException("Tried to create element with incorrect VR");
			if (AddElement(tag, vr)) {
				if (vr == DcmVR.IS)
					GetIS(tag).SetInt32(value);
				else
					GetSL(tag).SetValue(value);
				return true;
			}
			return false;
		}
		
		public bool AddElementWithValue(DcmTag tag, double value) {
			DcmVR vr = tag.Entry.DefaultVR;
			if (vr != DcmVR.DS && vr != DcmVR.FD)
				throw new DcmDataException("Tried to create element with incorrect VR");
			if (AddElement(tag, vr)) {
				if (vr == DcmVR.DS)
					GetDS(tag).SetDouble(value);
				else
					GetFD(tag).SetValue(value);
				return true;
			}
			return false;
		}

		public bool AddElementWithValue(DcmTag tag, decimal value) {
			DcmVR vr = tag.Entry.DefaultVR;
			if (vr != DcmVR.DS)
				throw new DcmDataException("Tried to create element with incorrect VR");
			if (AddElement(tag, vr)) {
				GetDS(tag).SetDecimal(value);
				return true;
			}
			return false;
		}

		public bool AddElementWithValue(DcmTag tag, DcmUID value) {
			return AddElementWithValue(tag, value.UID);
		}

		public bool AddElementWithObjectValue(DcmTag tag, object value) {
			DcmVR vr = tag.Entry.DefaultVR;
			if (AddElement(tag, vr)) {
				GetElement(tag).SetValueObject(value);
				return true;
			}
			return false;
		}

		public bool AddElementWithValueString(DcmTag tag, string value) {
			DcmVR vr = tag.Entry.DefaultVR;
			if (AddElement(tag, vr)) {
				GetElement(tag).SetValueString(value);
				return true;
			}
			return false;
		}

		public void AddItem(DcmItem item) {
			_items.Remove(item.Tag.Card);
			_items.Add(item.Tag.Card, item);
			item.SelectByteOrder(InternalTransferSyntax.Endian);
		}

		public DcmItem GetItem(DcmTag tag) {
			return _items[tag.Card];
		}

		public bool Contains(DcmTag tag) {
			return _items.ContainsKey(tag.Card);
		}

		public DcmVR GetVR(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem == null)
				return null;
			return elem.VR;
		}

		public DcmElement GetElement(DcmTag tag) {
			DcmItem item = null;
			if (!_items.TryGetValue(tag.Card, out item))
				return null;
			if (item is DcmElement)
				return item as DcmElement;
			return null;
		}

		public IEnumerable<DcmTag> GetMaskedTags(DcmTagMask mask) {
			for (int i = 0; i < _items.Values.Count; i++) {
				if (mask.IsMatch(_items.Values[i].Tag))
					yield return _items.Values[i].Tag;
			}
		}

		public DcmApplicationEntity GetAE(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmApplicationEntity)
				return elem as DcmApplicationEntity;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmAgeString GetAS(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmAgeString)
				return elem as DcmAgeString;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmAttributeTag GetAT(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmAttributeTag)
				return elem as DcmAttributeTag;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmCodeString GetCS(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmCodeString)
				return elem as DcmCodeString;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmDate GetDA(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmDate)
				return elem as DcmDate;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmDecimalString GetDS(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmDecimalString)
				return elem as DcmDecimalString;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmDateTime GetDT(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmDateTime)
				return elem as DcmDateTime;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmFloatingPointDouble GetFD(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmFloatingPointDouble)
				return elem as DcmFloatingPointDouble;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmFloatingPointSingle GetFL(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmFloatingPointSingle)
				return elem as DcmFloatingPointSingle;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmIntegerString GetIS(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmIntegerString)
				return elem as DcmIntegerString;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmLongString GetLO(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmLongString)
				return elem as DcmLongString;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmLongText GetLT(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmLongText)
				return elem as DcmLongText;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmOtherByte GetOB(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmOtherByte)
				return elem as DcmOtherByte;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmOtherFloat GetOF(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmOtherFloat)
				return elem as DcmOtherFloat;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmOtherWord GetOW(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmOtherWord)
				return elem as DcmOtherWord;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmPersonName GetPN(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmPersonName)
				return elem as DcmPersonName;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmShortString GetSH(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmShortString)
				return elem as DcmShortString;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmSignedLong GetSL(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmSignedLong)
				return elem as DcmSignedLong;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmItemSequence GetSQ(DcmTag tag) {
			DcmItem item = GetItem(tag);
			if (item is DcmItemSequence)
				return item as DcmItemSequence;
			if (item != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmSignedShort GetSS(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmSignedShort)
				return elem as DcmSignedShort;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmShortText GetST(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmShortText)
				return elem as DcmShortText;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmTime GetTM(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmTime)
				return elem as DcmTime;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmUniqueIdentifier GetUI(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmUniqueIdentifier)
				return elem as DcmUniqueIdentifier;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmUnsignedLong GetUL(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmUnsignedLong)
				return elem as DcmUnsignedLong;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmUnknown GetUN(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmUnknown)
				return elem as DcmUnknown;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmUnsignedShort GetUS(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmUnsignedShort)
				return elem as DcmUnsignedShort;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}

		public DcmUnlimitedText GetUT(DcmTag tag) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmUnlimitedText)
				return elem as DcmUnlimitedText;
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return null;
		}
		#endregion

		#region Data Access Methods
		public string GetString(DcmTag tag, string deflt) {
			return GetString(tag, 0, deflt);
		}

		public string GetString(DcmTag tag, int index, string deflt) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmStringElement)
				return (elem as DcmStringElement).GetValue(index);
			if (elem is DcmMultiStringElement)
				return (elem as DcmMultiStringElement).GetValue(index);
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return deflt;
		}

		public void SetString(DcmTag tag, string value) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmStringElement) {
				(elem as DcmStringElement).SetValue(value);
				return;
			}
			if (elem is DcmMultiStringElement) {
				(elem as DcmMultiStringElement).SetValue(value);
				return;
			}
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			throw new DcmDataException("Element does not exist in Dataset");
		}

		public string[] GetStringArray(DcmTag tag, string[] deflt) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmMultiStringElement)
				return (elem as DcmMultiStringElement).GetValues();
			if (elem is DcmStringElement)
				return new string[] { (elem as DcmStringElement).GetValue() };
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return deflt;
		}

		public void SetStringArray(DcmTag tag, string[] values) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmMultiStringElement) {
				(elem as DcmMultiStringElement).SetValues(values);
				return;
			}
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			throw new DcmDataException("Element does not exist in Dataset");
		}

		public DateTime GetDateTime(DcmTag tag, int index, DateTime deflt) {
			DcmElement elem = GetElement(tag);
			if (elem is DcmDate || elem is DcmTime || elem is DcmDateTime) {
				try {
					if (elem is DcmDate)
						return (elem as DcmDate).GetDateTime(index);
					if (elem is DcmTime)
						return (elem as DcmTime).GetDateTime(index);
					if (elem is DcmDateTime)
						return (elem as DcmDateTime).GetDateTime(index);
				}
				catch {
					return deflt;
				}
			}
			if (elem != null)
				throw new DcmDataException("Tried to access element with incorrect VR");
			return deflt;	
		}

		public DateTime GetDateTime(DcmTag dtag, DcmTag ttag, DateTime deflt) {
			DateTime dt;
			DcmDate da = GetDA(dtag);
			if (da != null)
				dt = da.GetDateTime(0);
			else
				dt = deflt;
			try {
				DcmTime tm = GetTM(ttag);
				if (tm != null) {
					DateTime time = tm.GetDateTime(0);
					dt = new DateTime(dt.Year, dt.Month, dt.Day, time.Hour, time.Minute, time.Second);
				}
			}
			catch { }
			return dt;
		}

		public DcmUID GetUID(DcmTag tag) {
			DcmUniqueIdentifier ui = GetUI(tag);
			if (ui != null)
				return ui.GetUID();
			return null;
		}

		public int GetInt32(DcmTag tag, int deflt) {
			DcmElement elem = GetElement(tag);
			if (elem != null) {
				if (elem.VR == DcmVR.IS)
					return (elem as DcmIntegerString).GetInt32();
				else if (elem.VR == DcmVR.SL)
					return (elem as DcmSignedLong).GetValue();
				else
					throw new DcmDataException("Tried to access element with incorrect VR");
			}
			return deflt;
		}

		public short GetInt16(DcmTag tag, short deflt) {
			DcmSignedShort ss = GetSS(tag);
			if (ss != null)
				return ss.GetValue();
			return deflt;
		}

		public ushort GetUInt16(DcmTag tag, ushort deflt) {
			DcmUnsignedShort us = GetUS(tag);
			if (us != null)
				return us.GetValue();
			return deflt;
		}

		public double GetDouble(DcmTag tag, double deflt) {
			DcmElement elem = GetElement(tag);
			if (elem != null) {
				if (elem.VR == DcmVR.FD)
					return (elem as DcmFloatingPointDouble).GetValue();
				else if (elem.VR == DcmVR.DS)
					return (elem as DcmDecimalString).GetDouble();
				else
					throw new DcmDataException("Tried to access element with incorrect VR");
			}
			return deflt;
		}

		public decimal GetDecimal(DcmTag tag, decimal deflt) {
			DcmDecimalString ds = GetDS(tag);
			if (ds != null)
				return ds.GetDecimal();
			return deflt;
		}
		#endregion

		#region Encoding
		public void ChangeTransferSyntax(DcmTS newTransferSyntax, DcmCodecParameters parameters) {
			DcmTS oldTransferSyntax = InternalTransferSyntax;

			if (oldTransferSyntax == newTransferSyntax)
				return;

			if (oldTransferSyntax.IsEncapsulated && newTransferSyntax.IsEncapsulated)
				ChangeTransferSyntax(DcmTS.ExplicitVRLittleEndian, null);

			DcmPixelData oldPixelData = null;
			DcmPixelData newPixelData = null;

			if (oldTransferSyntax.IsEncapsulated) {
				IDcmCodec codec = DicomCodec.GetCodec(oldTransferSyntax);
				oldPixelData = new DcmPixelData(this);
				newPixelData = new DcmPixelData(newTransferSyntax, oldPixelData);
				codec.Decode(this, oldPixelData, newPixelData, parameters);
				newPixelData.UpdateDataset(this);
				SetInternalTransferSyntax(newTransferSyntax);
			}
			else if (newTransferSyntax.IsEncapsulated) {
				IDcmCodec codec = DicomCodec.GetCodec(newTransferSyntax);
				oldPixelData = new DcmPixelData(this);
				newPixelData = new DcmPixelData(newTransferSyntax, oldPixelData);
				codec.Encode(this, oldPixelData, newPixelData, parameters);
				newPixelData.UpdateDataset(this);
				SetInternalTransferSyntax(newTransferSyntax);
			}
			else {
				SetInternalTransferSyntax(newTransferSyntax);
			}

			if (!newTransferSyntax.IsExplicitVR && GetVR(DcmTags.PixelData) != DcmVR.OW) {
				DcmOtherWord ow = new DcmOtherWord(DcmTags.PixelData);
				if (newPixelData == null)
					newPixelData = new DcmPixelData(this);
				if (newPixelData.BitsAllocated == 16) {
					for (int i = 0; i < newPixelData.NumberOfFrames; i++) {
						ushort[] data = newPixelData.GetFrameDataU16(i);
						for (int n = 0; n < data.Length; n++) {
							ow.ByteBuffer.Writer.Write(data[n]);
						}
					}
					if (oldTransferSyntax.Endian == Endian.Big)
						ow.ByteBuffer.Swap2();
				}
				else if (newPixelData.BitsAllocated == 8) {
					for (int i = 0; i < newPixelData.NumberOfFrames; i++) {
						byte[] data = newPixelData.GetFrameDataU8(i);
						ow.ByteBuffer.Writer.Write(data);
					}
					ow.ByteBuffer.Swap2();
				}
				else {
					throw new DcmDataException(
						String.Format("Unable to pixel data to OW:  bits allocated = {0}", newPixelData.BitsAllocated));
				}
				AddItem(ow);
			}
		}

		internal void SetInternalTransferSyntax(DcmTS ts) {
			_transferSyntax = ts;
			foreach (DcmItem item in _items.Values) {
				if (item is DcmElement) {
					item.SelectByteOrder(ts.Endian);
				}
				else if (item is DcmFragmentSequence) {
					item.SelectByteOrder(ts.Endian);
				}
				else if (item is DcmItemSequence) {
					DcmItemSequence sq = item as DcmItemSequence;
					sq.SelectByteOrder(ts.Endian);
					foreach (DcmItemSequenceItem si in sq.SequenceItems) {
						si.Dataset.SetInternalTransferSyntax(ts);
					}
				}
			}
		}
		#endregion

		#region Binding
		private object GetDefaultValue(Type vtype, DicomFieldDefault deflt) {
			try {
				if (vtype == typeof(DcmUID) || vtype == typeof(DcmTS) || vtype.IsSubclassOf(typeof(DcmElement)))
					return null;
				if (deflt == DicomFieldDefault.Null || deflt == DicomFieldDefault.None)
					return null;
				if (deflt == DicomFieldDefault.DBNull)
					return DBNull.Value;
				if (deflt == DicomFieldDefault.Default && vtype != typeof(string))
					return Activator.CreateInstance(vtype);
				if (vtype == typeof(string)) {
					if (deflt == DicomFieldDefault.StringEmpty || deflt == DicomFieldDefault.Default)
						return String.Empty;
				} else if (vtype == typeof(DateTime)) {
					if (deflt == DicomFieldDefault.DateTimeNow)
						return DateTime.Now;
					if (deflt == DicomFieldDefault.MinValue)
						return DateTime.MinValue;
					if (deflt == DicomFieldDefault.MaxValue)
						return DateTime.MaxValue;
				} else if (vtype.IsSubclassOf(typeof(ValueType))) {
					if (deflt == DicomFieldDefault.MinValue) {
						PropertyInfo pi = vtype.GetProperty("MinValue", BindingFlags.Static);
						if (pi != null) return pi.GetValue(null, null);
					}
					if (deflt == DicomFieldDefault.MaxValue) {
						PropertyInfo pi = vtype.GetProperty("MaxValue", BindingFlags.Static);
						if (pi != null) return pi.GetValue(null, null);
					}
					return Activator.CreateInstance(vtype);
				}
				return null;
			}
			catch (Exception) {
				Debug.Log.Error("Error in default value type! - {0}", vtype.ToString());
				return null;
			}
		}

		private object LoadDicomFieldValue(DcmElement elem, Type vtype, DicomFieldDefault deflt, bool udzl) {
			if (vtype.IsSubclassOf(typeof(DcmElement))) {
				if (elem != null && vtype != elem.GetType())
					throw new DcmDataException("Invalid binding type for Element VR!");
				return elem;
			} else if (vtype.IsArray) {
				if (elem != null) {
					if (vtype.GetElementType() != elem.GetValueType())
						throw new DcmDataException("Invalid binding type for Element VR!");
					if (elem.GetValueType() == typeof(DateTime))
						return (elem as DcmDateElementBase).GetDateTimes();
					else
						return elem.GetValueObjectArray();
				} else {
					if (deflt == DicomFieldDefault.EmptyArray)
						return Array.CreateInstance(vtype, 0);
					else
						return null;
				}
			} else {
				if (elem != null) {
					if (elem.Length == 0 && udzl) {
						return GetDefaultValue(vtype, deflt);
					}
					if (vtype != elem.GetValueType()) {
						if (vtype == typeof(string)) {
							return elem.GetValueString();
						} else if (vtype == typeof(DcmUID) && elem.VR == DcmVR.UI) {
							return (elem as DcmUniqueIdentifier).GetUID();
						} else if (vtype == typeof(DcmTS) && elem.VR == DcmVR.UI) {
							return (elem as DcmUniqueIdentifier).GetTS();
						} else if (vtype == typeof(DcmDateRange) && elem.GetType().IsSubclassOf(typeof(DcmDateElementBase))) {
							return (elem as DcmDateElementBase).GetDateTimeRange();
						} else if (vtype == typeof(object)) {
							return elem.GetValueObject();
						} else
							throw new DcmDataException("Invalid binding type for Element VR!");
					} else {
						return elem.GetValueObject();
					}
				} else {
					return GetDefaultValue(vtype, deflt);
				}
			}
		}

		public void LoadDicomFields(object obj) {
			FieldInfo[] fields = obj.GetType().GetFields();
			foreach (FieldInfo field in fields) {
				if (field.IsDefined(typeof(DicomFieldAttribute), true)) {
					try {
						DicomFieldAttribute dfa = (DicomFieldAttribute)field.GetCustomAttributes(typeof(DicomFieldAttribute), true)[0];
						DcmElement elem = GetElement(dfa.Tag);
						if ((elem == null || (elem.Length == 0 && dfa.UseDefaultForZeroLength)) && dfa.DefaultValue == DicomFieldDefault.None) {
							// do nothing
						}
						else {
							field.SetValue(obj, LoadDicomFieldValue(elem, field.FieldType, dfa.DefaultValue, dfa.UseDefaultForZeroLength));
						}
					}
					catch (Exception e) {
						Debug.Log.Debug("Unable to bind field: " + e.Message);
					}
				}
			}

			PropertyInfo[] properties = obj.GetType().GetProperties();
			foreach (PropertyInfo property in properties) {
				if (property.IsDefined(typeof(DicomFieldAttribute), true)) {
					try {
						DicomFieldAttribute dfa = (DicomFieldAttribute)property.GetCustomAttributes(typeof(DicomFieldAttribute), true)[0];
						DcmElement elem = GetElement(dfa.Tag);
						if ((elem == null || (elem.Length == 0 && dfa.UseDefaultForZeroLength)) && dfa.DefaultValue == DicomFieldDefault.None) {
							// do nothing
						} else {
							property.SetValue(obj, LoadDicomFieldValue(elem, property.PropertyType, dfa.DefaultValue, dfa.UseDefaultForZeroLength), null);
						}
					}
					catch (Exception e) {
						Debug.Log.Debug("Unable to bind field: " + e.Message);
					}
				}
			}
		}

		private void SaveDicomFieldValue(DcmTag tag, object value, bool createEmpty) {
			if (value != null && value != DBNull.Value) {
				Type vtype = value.GetType();
				if (vtype.IsSubclassOf(typeof(DcmElement))) {
					AddItem((DcmItem)value);
				} else {
					if (!Contains(tag)) {
						AddElement(tag, tag.Entry.DefaultVR);
					}
					DcmElement elem = GetElement(tag);
					if (vtype.IsArray) {
						if (vtype.GetElementType() != elem.GetValueType())
							throw new DcmDataException("Invalid binding type for Element VR!");
						if (elem.GetValueType() == typeof(DateTime))
							(elem as DcmDateElementBase).SetDateTimes((DateTime[])value);
						else
							elem.SetValueObjectArray((object[])value);
					} else {
						if (elem.VR == DcmVR.UI && vtype == typeof(DcmUID)) {
							DcmUID ui = (DcmUID)value;
							elem.SetValueString(ui.UID);
						} else if (elem.VR == DcmVR.UI && vtype == typeof(DcmTS)) {
							DcmTS ts = (DcmTS)value;
							elem.SetValueString(ts.UID.UID);
						}
						else if (vtype == typeof(DcmDateRange) && elem.GetType().IsSubclassOf(typeof(DcmDateElementBase))) {
							DcmDateRange dr = (DcmDateRange)value;
							(elem as DcmDateElementBase).SetDateTimeRange(dr);
						} else if (vtype != elem.GetValueType()) {
							if (vtype == typeof(string)) {
								elem.SetValueString((string)value);
							} else
								throw new DcmDataException("Invalid binding type for Element VR!");
						} else {
							elem.SetValueObject(value);
						}
					}
				}
			} else {
				if (Contains(tag)) {
					GetElement(tag).ByteBuffer.Clear();
				} else if (createEmpty) {
					AddElement(tag, tag.Entry.DefaultVR);
				}
			}
		}

		public void SaveDicomFields(object obj) {
			FieldInfo[] fields = obj.GetType().GetFields();
			foreach (FieldInfo field in fields) {
				if (field.IsDefined(typeof(DicomFieldAttribute), true)) {
					DicomFieldAttribute dfa = (DicomFieldAttribute)field.GetCustomAttributes(typeof(DicomFieldAttribute), true)[0];
					object value = field.GetValue(obj);
					SaveDicomFieldValue(dfa.Tag, value, dfa.CreateEmptyElement);
				}
			}

			PropertyInfo[] properties = obj.GetType().GetProperties();
			foreach (PropertyInfo property in properties) {
				if (property.IsDefined(typeof(DicomFieldAttribute), true)) {
					DicomFieldAttribute dfa = (DicomFieldAttribute)property.GetCustomAttributes(typeof(DicomFieldAttribute), true)[0];
					object value = property.GetValue(obj, null);
					SaveDicomFieldValue(dfa.Tag, value, dfa.CreateEmptyElement);
				}
			}
		}
		#endregion

		#region Dump
		public string Dump() {
			StringBuilder sb = new StringBuilder();
			Dump(sb, "", DicomDumpOptions.Default);
			return sb.ToString();
		}

		public void Dump(StringBuilder sb, String prefix, DicomDumpOptions options) {
			foreach (DcmItem item in _items.Values) {
				item.Dump(sb, prefix, DicomDumpOptions.Default);
				sb.AppendLine();
			}
		}
		#endregion
	}
}