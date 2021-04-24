using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ConfusableMatcherCSInterop
{
	public struct CMOptions
	{
		public bool MatchRepeating;
		public nuint StartIndex;
		public bool StartFromEnd;
		public nuint StatePushLimit;
		public bool MatchOnWordBoundary;

		public CMOptions(bool MatchRepeating, nuint StartIndex, bool StartFromEnd, nuint StatePushLimit, bool MatchOnWordBoundary)
		{
			this.MatchRepeating = MatchRepeating;
			this.StartIndex = StartIndex;
			this.StartFromEnd = StartFromEnd;
			this.StatePushLimit = StatePushLimit;
			this.MatchOnWordBoundary = MatchOnWordBoundary;
		}

		public static CMOptions Default => new CMOptions(false, 0, false, 1000, false);
	}

	public class ConfusableMatcher : IDisposable
	{
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		struct CMKV
		{
			[MarshalAs(UnmanagedType.LPUTF8Str)]
			public string Key;

			[MarshalAs(UnmanagedType.LPUTF8Str)]
			public string Value;
		}

		struct CMMap
		{
			public IntPtr Kv;
			public uint Size;
		}

		public enum CM_RETURN_STATUS
		{
			MATCH = 0,
			NO_MATCH = 1,
			STATE_PUSH_LIMIT_EXCEEDED = 2,
			WORD_BOUNDARY_FAIL_START = 3,
			WORD_BOUNDARY_FAIL_END = 4
		}

		public struct CMReturn
		{
			public ulong Start;
			public ulong Size;
			public CM_RETURN_STATUS Status;

			public CMReturn(ulong Start, ulong Size, CM_RETURN_STATUS Status)
			{
				this.Start = Start;
				this.Size = Size;
				this.Status = Status;
			}
		}

		private readonly IntPtr CMHandle;
		private int Freed = 0;

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr InitConfusableMatcher(CMMap Map, IntPtr IgnoreList, int IgnoreCount, bool AddDefaultValues);

		[SkipLocalsInit]
		public unsafe ConfusableMatcher(IList<(string Key, string Value)> Map, string[]? IgnoreList, bool AddDefaultValues = true)
		{
			var cmmap = new CMMap();
			var kvs = new CMKV[Map.Count];
			int x = 0;

			foreach (var kv in Map) {
				CMKV cmkv = new CMKV() {
					Key = kv.Key,
					Value = kv.Value
				};

				kvs[x++] = cmkv;
			}

			var kvPtr = ((Memory<byte>)new byte[Marshal.SizeOf<CMKV>() * kvs.Length]).Pin();

			cmmap.Kv = new IntPtr(kvPtr.Pointer);

			for (x = 0;x < kvs.Length;x++) {
				Marshal.StructureToPtr(kvs[x], cmmap.Kv + (x * Marshal.SizeOf(typeof(CMKV))), false);
			}

			cmmap.Size = (uint)kvs.Length;

			var free = new IDisposable[IgnoreList?.Length ?? 0];
			var listPtr = ((Memory<byte>)new byte[Marshal.SizeOf(typeof(IntPtr)) * (IgnoreList?.Length ?? 1)]).Pin();

			var intPtrList = new IntPtr(listPtr.Pointer);

			for (x = 0;x < (IgnoreList?.Length ?? 0);x++) {
				fixed (char* pIn = IgnoreList[x]) {
					int len = Encoding.UTF8.GetByteCount(pIn, IgnoreList[x].Length);
					var ptrIgnore = ((Memory<byte>)new byte[len + 1]).Pin();
					free[x] = ptrIgnore;

					Encoding.UTF8.GetBytes(pIn, IgnoreList[x].Length, (byte*)ptrIgnore.Pointer, len);

					Marshal.WriteIntPtr(intPtrList + (x * sizeof(IntPtr)), new IntPtr(ptrIgnore.Pointer));
				}
			}

			CMHandle = InitConfusableMatcher(cmmap, intPtrList, IgnoreList?.Length ?? 0, AddDefaultValues);

			for (x = 0;x < kvs.Length;x++) {
				Marshal.DestroyStructure<CMKV>(cmmap.Kv + (x * Marshal.SizeOf(typeof(CMKV))));
			}

			listPtr.Dispose();
			kvPtr.Dispose();
			foreach (var f in free)
				f.Dispose();
		}

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern CMReturn StringIndexOf(IntPtr CM, byte *In, [MarshalAs(UnmanagedType.LPUTF8Str)] string Contains, CMOptions Options);

		public CMReturn IndexOf(ReadOnlySpan<char> In, string Contains, CMOptions Options)
		{
			if (Options.StartIndex != 0) {
				// Convert StartIndex in UTF8 terms
				var startSkip = In[..(int)Options.StartIndex];
				Options.StartIndex = (nuint)Encoding.UTF8.GetByteCount(startSkip);
			}

			int inUtf8len = Encoding.UTF8.GetMaxByteCount(In.Length);
			Span<byte> utf8In = stackalloc byte[inUtf8len + 1];
			Encoding.UTF8.GetBytes(In, utf8In);

			var ret = IndexOf(utf8In, Contains, Options);

			if (ret.Start >= 0) {
				var start = utf8In[..(int)ret.Start];
				var matchedPart = utf8In[(int)ret.Start..((int)ret.Start+(int)ret.Size)];

				return new CMReturn((ulong)Encoding.UTF8.GetCharCount(start), (ulong)Encoding.UTF8.GetCharCount(matchedPart), ret.Status);
			}

			return ret;
		}

		public unsafe CMReturn IndexOf(Span<byte> In, string Contains, CMOptions Options)
		{
			var res = StringIndexOf(CMHandle, (byte*)Unsafe.AsPointer(ref In.GetPinnableReference()), Contains, Options);
			return res;
		}

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern uint GetKeyMappings(IntPtr CM, [MarshalAs(UnmanagedType.LPUTF8Str)] string In, byte* Output, uint OutputSize);
		public unsafe List<string> GetKeyMappings(string In)
		{
			byte* outputPtr = stackalloc byte[IntPtr.Size * 32];

			var sz = GetKeyMappings(CMHandle, In, outputPtr, 32);

			if (sz > 32) {
				Span<byte> buffer = new byte[IntPtr.Size * sz];
				outputPtr = (byte*)Unsafe.AsPointer(ref buffer.GetPinnableReference());

				_ = GetKeyMappings(CMHandle, In, outputPtr, sz);
			}

			var ret = new List<string>((int)sz);
			for (var x = 0;x < sz;x++) {
				var arrPtr = (byte**)outputPtr;

				int strSz = 0;
				while (arrPtr[x][++strSz] != 0x00);

				var copy = Encoding.UTF8.GetString(arrPtr[x], strSz);
				ret.Add(copy);
			}

			return ret;
		}

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static extern void FreeConfusableMatcher(IntPtr In);

		public void Dispose()
		{
			var old = Interlocked.CompareExchange(ref Freed, 1, 0);
			if (old == 0) {
				FreeConfusableMatcher(CMHandle);
			}
		}

		~ConfusableMatcher()
		{
			var old = Interlocked.CompareExchange(ref Freed, 1, 0);
			if (old == 0) {
				FreeConfusableMatcher(CMHandle);
			}
		}
	}
}
