using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CMHandle = System.IntPtr;
using CMListHandle = System.IntPtr;

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

		public enum MAPPING_RESPONSE
		{
			SUCCESS = 0,
			ALREADY_EXISTS = 1,
			EMPTY_KEY = 2,
			EMPTY_VALUE = 3,
			INVALID_KEY = 4,
			INVALID_VALUE = 5
		}

		private readonly CMHandle CMHandle;
		private CMListHandle CMIgnoreList = IntPtr.Zero;
		private int Freed = 0;

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static extern CMHandle InitConfusableMatcher(CMMap Map, bool AddDefaultValues);
		public unsafe ConfusableMatcher(IList<(ReadOnlyMemory<char> Key, ReadOnlyMemory<char> Value)> Map, bool AddDefaultValues = true)
		{
			var cmmap = new CMMap();
			var kvs = new CMKV[Map.Count];
			int x = 0;

			foreach (var kv in Map) {
				CMKV cmkv;

				fixed (char* pKey = &MemoryMarshal.GetReference(kv.Key.Span)) {
					int keyLen = Encoding.UTF8.GetByteCount(pKey, kv.Key.Length);
					var keyBuffer = stackalloc byte[keyLen + 1];
					Encoding.UTF8.GetBytes(pKey, kv.Key.Length, keyBuffer, keyLen);

					cmkv.Key = keyBuffer;
				}

				fixed (char* pValue = &MemoryMarshal.GetReference(kv.Value.Span)) {
					int valLen = Encoding.UTF8.GetByteCount(pValue, kv.Value.Length);
					var valBuffer = stackalloc byte[valLen + 1];
					Encoding.UTF8.GetBytes(pValue, kv.Value.Length, valBuffer, valLen);
				
					cmkv.Value = valBuffer;
				}

				kvs[x++] = cmkv;
			}

			cmmap.Kv = Marshal.AllocHGlobal(Marshal.SizeOf<CMKV>() * kvs.Length);

			long ptrMem = cmmap.Kv.ToInt64(); // Must work both on x86 and x64
			for (x = 0;x < kvs.Length;x++) {
				Marshal.StructureToPtr(kvs[x], new IntPtr(ptrMem), false);
				ptrMem += Marshal.SizeOf(typeof(CMKV));
			}

			cmmap.Size = (uint)kvs.Length;

			CMHandle = InitConfusableMatcher(cmmap, AddDefaultValues);

			Marshal.FreeHGlobal(cmmap.Kv);

			SetIgnoreList(new string[] { });
		}

		public unsafe ConfusableMatcher(IList<(string Key, string Value)> Map, bool AddDefaultValues = true) : this(Map.Select(x => (x.Key.AsMemory(), x.Value.AsMemory())).ToList(), AddDefaultValues) { }


		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static extern CMHandle ConstructIgnoreList(IntPtr List, int Count);
		public unsafe void SetIgnoreList(IList<ReadOnlyMemory<char>> In)
		{
			if (CMIgnoreList != IntPtr.Zero)
				FreeIgnoreList(CMIgnoreList);

			var list = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * In.Count);
			for (int x = 0;x < In.Count;x++) {
				fixed (char* pIn = &MemoryMarshal.GetReference(In[x].Span)) {
					int len = Encoding.UTF8.GetByteCount(pIn, In[x].Length);
					var buffer = stackalloc byte[len + 1];
					Encoding.UTF8.GetBytes(pIn, In[x].Length, buffer, len);
					Marshal.WriteIntPtr(list + (x * sizeof(IntPtr)), new IntPtr(buffer));
				}
			}

			CMIgnoreList = ConstructIgnoreList(list, In.Count);
		}

		public void SetIgnoreList(IList<string> In) => SetIgnoreList(In.Select(x => x.AsMemory()).ToList());

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern ulong StringIndexOf(CMHandle CM, byte *In, byte *Contains, bool MatchRepeating, int StartIndex, CMListHandle IgnoreList);

		public unsafe (int Index, int Length) IndexOf(ReadOnlyMemory<char> In, ReadOnlyMemory<char> Contains, bool MatchRepeating, int StartIndex)
		{
			fixed (char* ptrIn = &MemoryMarshal.GetReference(In.Span)) {
				int inLen = Encoding.UTF8.GetByteCount(ptrIn, In.Length);
				var inBuffer = stackalloc byte[inLen + 1];
				Encoding.UTF8.GetBytes(ptrIn, In.Length, inBuffer, inLen);
			

				fixed (char* ptrContains = &MemoryMarshal.GetReference(Contains.Span)) {
					int containsLen = Encoding.UTF8.GetByteCount(ptrContains, Contains.Length);
					var containsBuffer = stackalloc byte[containsLen + 1];
					Encoding.UTF8.GetBytes(ptrContains, Contains.Length, containsBuffer, containsLen);
			
					var res = StringIndexOf(CMHandle, inBuffer, containsBuffer, MatchRepeating, StartIndex, CMIgnoreList);
					return ((int)(res & 0xFFFFFFFF), (int)(res >> 32));
				}
			}
		}

		public (int Index, int Length) IndexOf(string In, string Contains, bool MatchRepeating, int StartIndex) => IndexOf(In.AsMemory(), Contains.AsMemory(), MatchRepeating, StartIndex);

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern MAPPING_RESPONSE AddMapping(CMHandle CM, byte* Key, byte* Value, bool CheckValueDuplicate);

		public unsafe MAPPING_RESPONSE AddMapping(ReadOnlyMemory<char> Key, ReadOnlyMemory<char> Value, bool CheckValueDuplicate = true)
		{
			fixed (char* ptrKey = &MemoryMarshal.GetReference(Key.Span)) {
				int keyLen = Encoding.UTF8.GetByteCount(ptrKey, Key.Length);
				var keyBuffer = stackalloc byte[keyLen + 1];
				Encoding.UTF8.GetBytes(ptrKey, Key.Length, keyBuffer, keyLen);

				fixed (char* ptrValue = &MemoryMarshal.GetReference(Value.Span)) {
					int valLen = Encoding.UTF8.GetByteCount(ptrValue, Value.Length);
					var valBuffer = stackalloc byte[valLen + 1];
					Encoding.UTF8.GetBytes(ptrValue, Value.Length, valBuffer, valLen);

					return AddMapping(CMHandle, keyBuffer, valBuffer, CheckValueDuplicate);
				}
			}
		}

		public MAPPING_RESPONSE AddMapping(string Key, string Value, bool CheckValueDuplicate = true) => AddMapping(Key.AsMemory(), Value.AsMemory(), CheckValueDuplicate);

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static unsafe extern bool RemoveMapping(CMHandle CM, byte* Key, byte* Value);

		public unsafe bool RemoveMapping(ReadOnlyMemory<char> Key, ReadOnlyMemory<char> Value)
		{
			fixed (char* ptrKey = &MemoryMarshal.GetReference(Key.Span)) {
				int keyLen = Encoding.UTF8.GetByteCount(ptrKey, Key.Length);
				var keyBuffer = stackalloc byte[keyLen + 1];
				Encoding.UTF8.GetBytes(ptrKey, Key.Length, keyBuffer, keyLen);

				fixed (char* ptrValue = &MemoryMarshal.GetReference(Value.Span)) {
					int valLen = Encoding.UTF8.GetByteCount(ptrValue, Value.Length);
					var valBuffer = stackalloc byte[valLen + 1];
					Encoding.UTF8.GetBytes(ptrValue, Value.Length, valBuffer, valLen);

					return RemoveMapping(CMHandle, keyBuffer, valBuffer);
				}
			}
		}

		public unsafe bool RemoveMapping(string Key, string Value) => RemoveMapping(Key.AsMemory(), Value.AsMemory());

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static extern void FreeConfusableMatcher(CMHandle In);
		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static extern CMHandle FreeIgnoreList(CMListHandle List);

		public void Dispose()
		{
			var old = Interlocked.CompareExchange(ref Freed, 1, 0);
			if (old == 0) {
				FreeConfusableMatcher(CMHandle);
				FreeIgnoreList(CMIgnoreList);
			}
		}

		~ConfusableMatcher()
		{
			var old = Interlocked.CompareExchange(ref Freed, 1, 0);
			if (old == 0) {
				FreeConfusableMatcher(CMHandle);
				FreeIgnoreList(CMIgnoreList);
			}
		}
    }
}
