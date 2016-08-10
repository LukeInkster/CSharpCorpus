// ZipFileInfo.cs created with MonoDevelop
// User: alan at 12:14 13/10/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;
using System.Runtime.InteropServices;

namespace zipsharp
{
	[StructLayoutAttribute (LayoutKind.Sequential)]
	struct ZipFileInfo
	{
		ZipTime date;
		IntPtr dosDate;
		IntPtr internalFileAttributes;
		IntPtr externalFileAttributes;

		public DateTime FileTime
		{
			get { return date.Date; }
		}

		public long DosDate
		{
			get { return dosDate.ToInt64 (); }
		}
		
		internal long InternalFileAttributes
		{
			get { return internalFileAttributes.ToInt64 (); }
		}

		internal long ExternalFileAttributes
		{
			get { return externalFileAttributes.ToInt64 (); }
		}
		
		public ZipFileInfo (DateTime fileTime)
		{
			date = new ZipTime (fileTime);
			dosDate = IntPtr.Zero;
			internalFileAttributes = IntPtr.Zero;
			externalFileAttributes = IntPtr.Zero;
		}

	}
}
