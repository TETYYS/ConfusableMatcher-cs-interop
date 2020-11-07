using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ConfusableMatcherCSInterop
{
    public class ConfusableMatcher : IDisposable
    {
		unsafe struct CMKV
		{
			public byte *Key;
			public byte *Value;
		}

		struct CMMap
		{
			public IntPtr Kv; // CMKV
			public uint Size;
		}

		private readonly IntPtr CMHandle;
        private int Freed = 0;

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr InitConfusableMatcher(CMMap Map, IntPtr IgnoreList, int IgnoreCount, bool AddDefaultValues);
		public unsafe ConfusableMatcher(IList<(ReadOnlyMemory<char> Key, ReadOnlyMemory<char> Value)> Map, ReadOnlyMemory<ReadOnlyMemory<char>>? IgnoreList, bool AddDefaultValues = true)
		{
			var cmmap = new CMMap();
			var kvs = new CMKV[Map.Count];
			int x = 0;

			var free = new List<IDisposable>();

			foreach (var kv in Map) {
				CMKV cmkv;

				fixed (char* pKey = kv.Key.Span) {
					int keyLen = Encoding.UTF8.GetByteCount(pKey, kv.Key.Length);
					var keyPtr = ((Memory<byte>)new byte[keyLen + 1]).Pin();
					free.Add(keyPtr);

					Encoding.UTF8.GetBytes(pKey, kv.Key.Length, (byte*)keyPtr.Pointer, keyLen);

					cmkv.Key = (byte*)keyPtr.Pointer;
				}

				fixed (char* pValue = kv.Value.Span) {
					int valLen = Encoding.UTF8.GetByteCount(pValue, kv.Value.Length);
					var valPtr = ((Memory<byte>)new byte[valLen + 1]).Pin();
					free.Add(valPtr);

					Encoding.UTF8.GetBytes(pValue, kv.Value.Length, (byte*)valPtr.Pointer, valLen);

					cmkv.Value = (byte*)valPtr.Pointer;
				}

				kvs[x++] = cmkv;
			}

			var kvPtr = ((Memory<byte>)new byte[Marshal.SizeOf<CMKV>() * kvs.Length]).Pin();
			free.Add(kvPtr);

			cmmap.Kv = new IntPtr(kvPtr.Pointer);

			for (x = 0;x < kvs.Length;x++) {
				Marshal.StructureToPtr(kvs[x], cmmap.Kv + (x * Marshal.SizeOf(typeof(CMKV))), false);
			}

			cmmap.Size = (uint)kvs.Length;

			var listPtr = ((Memory<byte>)new byte[Marshal.SizeOf(typeof(IntPtr)) * (IgnoreList?.Length ?? 1)]).Pin();
			free.Add(listPtr);

			var intPtrList = new IntPtr(listPtr.Pointer);

			for (x = 0;x < (IgnoreList?.Length ?? 0);x++) {
				fixed (char* pIn = IgnoreList.Value.Span[x].Span) {
					int len = Encoding.UTF8.GetByteCount(pIn, IgnoreList.Value.Span[x].Length);
					var ptrIgnore = ((Memory<byte>)new byte[len + 1]).Pin();
					free.Add(ptrIgnore);

					Encoding.UTF8.GetBytes(pIn, IgnoreList.Value.Span[x].Length, (byte*)ptrIgnore.Pointer, len);

					Marshal.WriteIntPtr(intPtrList + (x * sizeof(IntPtr)), new IntPtr(ptrIgnore.Pointer));
				}
			}

			CMHandle = InitConfusableMatcher(cmmap, intPtrList, IgnoreList?.Length ?? 0, AddDefaultValues);

			foreach (var f in free)
				f.Dispose();
		}

		public unsafe ConfusableMatcher(IList<(string Key, string Value)> Map, string[] IgnoreList, bool AddDefaultValues = true) : this(Map.Select(x => (x.Key.AsMemory(), x.Value.AsMemory())).ToList(), IgnoreList?.Select(x => x.AsMemory()).ToArray(), AddDefaultValues) { }

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern ulong StringIndexOf(IntPtr CM, byte *In, byte *Contains, bool MatchRepeating, int StartIndex, int StatePushLimit);

		public (int Index, int Length) IndexOf(ReadOnlySpan<char> In, ReadOnlySpan<char> Contains, bool MatchRepeating, int StartIndex, int StatePushLimit = 1000)
		{
			// TODO: Utf8String

			if (StartIndex != 0) {
				// Convert StartIndex in UTF8 terms
				var startSkip = In[..StartIndex];
				StartIndex = Encoding.UTF8.GetByteCount(startSkip);
			}

			int inUtf8len = Encoding.UTF8.GetMaxByteCount(In.Length);
			Span<byte> utf8In = stackalloc byte[inUtf8len + 1];
			Encoding.UTF8.GetBytes(In, utf8In);

			int containsUtf8Len = Encoding.UTF8.GetMaxByteCount(Contains.Length);
			Span<byte> utf8Contains = stackalloc byte[containsUtf8Len + 1];
			Encoding.UTF8.GetBytes(Contains, utf8Contains);

			var ret = IndexOf(utf8In, utf8Contains, MatchRepeating, StartIndex, StatePushLimit);

			if (ret.Index >= 0) {
				var start = utf8In[..ret.Index];
				var matchedPart = utf8In[ret.Index..(ret.Index+ret.Length)];

				return (Encoding.UTF8.GetCharCount(start), Encoding.UTF8.GetCharCount(matchedPart));
			}

			return ret;
		}

		public unsafe (int Index, int Length) IndexOf(Span<byte> In, Span<byte> Contains, bool MatchRepeating, int StartIndexUtf8, int StatePushLimit = 1000)
		{
			var res = StringIndexOf(CMHandle, (byte*)Unsafe.AsPointer(ref In.GetPinnableReference()), (byte*)Unsafe.AsPointer(ref Contains.GetPinnableReference()), MatchRepeating, StartIndexUtf8, StatePushLimit);
			return ((int)(res & 0xFFFFFFFF), (int)(res >> 32));
		}

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern uint GetKeyMappings(IntPtr CM, byte *In, byte *Output, uint OutputSize);
		public unsafe List<string> GetKeyMappings(Span<byte> In)
		{
			byte* outputPtr = stackalloc byte[IntPtr.Size * 32];
			var inPtr = (byte*)Unsafe.AsPointer(ref In.GetPinnableReference());
			MemoryHandle? memHandle = null;

			var sz = GetKeyMappings(CMHandle, inPtr, outputPtr, 32);

			if (sz > 32) {
				Memory<byte> buffer = new byte[IntPtr.Size * sz];
				memHandle = buffer.Pin();

				outputPtr = (byte*)memHandle.Value.Pointer;
				_ = GetKeyMappings(CMHandle, inPtr, outputPtr, sz);
			}

			var ret = new List<string>((int)sz);
			for (var x = 0;x < sz;x++) {
				var arrPtr = (byte**)outputPtr;

				int strSz = 0;
				while (arrPtr[x][++strSz] != 0x00);

				var copy = Encoding.UTF8.GetString(arrPtr[x], strSz);
				ret.Add(copy);
			}

			memHandle?.Dispose();

			return ret;
		}

		public List<string> GetKeyMappings(string In)
		{
			int inUtf8len = Encoding.UTF8.GetMaxByteCount(In.Length);
			Span<byte> utf8In = stackalloc byte[inUtf8len + 1];
			Encoding.UTF8.GetBytes(In, utf8In);

			return GetKeyMappings(utf8In);
		}

		public (int Index, int Length) IndexOf(string In, string Contains, bool MatchRepeating, int StartIndex, int StatePushLimit = 1000) => IndexOf(In, (ReadOnlySpan<char>)Contains, MatchRepeating, StartIndex, StatePushLimit);

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
