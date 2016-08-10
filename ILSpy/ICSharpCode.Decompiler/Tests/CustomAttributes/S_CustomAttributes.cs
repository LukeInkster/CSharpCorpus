﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;

namespace aa
{
	public static class CustomAttributes
	{
		[Flags]
		public enum EnumWithFlag
		{
			All = 15,
			None = 0,
			Item1 = 1,
			Item2 = 2,
			Item3 = 4,
			Item4 = 8
		}
		[AttributeUsage(AttributeTargets.All)]
		public class MyAttribute : Attribute
		{
			public MyAttribute(object val)
			{
			}
		}
		[CustomAttributes.MyAttribute(CustomAttributes.ULongEnum.MaxUInt64)]
		public enum ULongEnum : ulong
		{
			MaxUInt64 = 18446744073709551615uL
		}
		[CustomAttributes.MyAttribute(CustomAttributes.EnumWithFlag.Item1 | CustomAttributes.EnumWithFlag.Item2)]
		private static int field;
		[CustomAttributes.MyAttribute(CustomAttributes.EnumWithFlag.All)]
		public static string Property
		{
			get
			{
				return "aa";
			}
		}
		[Obsolete("some message")]
		public static void ObsoletedMethod()
		{
			//Console.WriteLine("{0} $$$ {1}", AttributeTargets.Interface, (AttributeTargets)(AttributeTargets.Property | AttributeTargets.Field));
			Console.WriteLine("{0} $$$ {1}", AttributeTargets.Interface, AttributeTargets.Property | AttributeTargets.Field);
			AttributeTargets attributeTargets = AttributeTargets.Property | AttributeTargets.Field;
			Console.WriteLine("{0} $$$ {1}", AttributeTargets.Interface, attributeTargets);
		}
		// No Boxing
		[CustomAttributes.MyAttribute(new StringComparison[]
		{
			StringComparison.Ordinal, 
			StringComparison.CurrentCulture
		})]
		public static void ArrayAsAttribute1()
		{
		}
		// Boxing of each array element
		[CustomAttributes.MyAttribute(new object[]
		{
			StringComparison.Ordinal, 
			StringComparison.CurrentCulture
		})]
		public static void ArrayAsAttribute2()
		{
		}
	}
}
