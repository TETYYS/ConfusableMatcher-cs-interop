using System;
using System.Collections.Generic;
using System.Linq;
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

		public unsafe (int Index, int Length) IndexOf(ReadOnlySpan<char> In, ReadOnlySpan<char> Contains, bool MatchRepeating, int StartIndex, int StatePushLimit = 1000)
		{
			fixed (char* pUtf16In = &MemoryMarshal.GetReference(In)) {
				int inUtf8len = Encoding.UTF8.GetByteCount(pUtf16In, In.Length);
				var pUtf8In = stackalloc byte[inUtf8len + 1];
				Encoding.UTF8.GetBytes(pUtf16In, In.Length, pUtf8In, inUtf8len);

				fixed (char* pUtf16Contains = &MemoryMarshal.GetReference(Contains)) {
					int containsUtf8Len = Encoding.UTF8.GetByteCount(pUtf16Contains, Contains.Length);
					var pUtf8Contains = stackalloc byte[containsUtf8Len + 1];
					Encoding.UTF8.GetBytes(pUtf16Contains, Contains.Length, pUtf8Contains, containsUtf8Len);

                    var res = StringIndexOf(CMHandle, pUtf8In, pUtf8Contains, MatchRepeating, StartIndex, StatePushLimit);
                    return ((int)(res & 0xFFFFFFFF), (int)(res >> 32));
				}
			}
		}

		public (int Index, int Length) IndexOf(string In, string Contains, bool MatchRepeating, int StartIndex) => IndexOf(In.AsMemory().Span, Contains.AsMemory().Span, MatchRepeating, StartIndex);

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
