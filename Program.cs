using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
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
		public unsafe ConfusableMatcher(List<(string Key, string Value)> Map, bool AddDefaultValues = true)
		{
			var cmmap = new CMMap();
			var kvs = new CMKV[Map.Count];
			int x = 0;

			foreach (var kv in Map) {
				CMKV cmkv;

				int keyLen = Encoding.UTF8.GetByteCount(kv.Key);
				var keyBuffer = stackalloc byte[keyLen + 1];
				fixed (char* ptr = kv.Key) {
					Encoding.UTF8.GetBytes(ptr, kv.Key.Length, keyBuffer, keyLen);
				}

				cmkv.Key = keyBuffer;

				int valLen = Encoding.UTF8.GetByteCount(kv.Value);
				var valBuffer = stackalloc byte[valLen + 1];
				fixed (char* ptr = kv.Value) {
					Encoding.UTF8.GetBytes(ptr, kv.Value.Length, valBuffer, valLen);
				}

				cmkv.Value = valBuffer;

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


		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static extern CMHandle ConstructIgnoreList(IntPtr List, int Count);
		public unsafe void SetIgnoreList(IList<string> In)
		{
			if (CMIgnoreList != IntPtr.Zero)
				FreeIgnoreList(CMIgnoreList);
			
			var list = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * In.Count);
			for (int x = 0;x < In.Count;x++) {
				int len = Encoding.UTF8.GetByteCount(In[x]);
				var buffer = new byte[len + 1];
				fixed (char* ptr = In[x]) {
					fixed (byte *ptrBuffer = buffer) {
						Encoding.UTF8.GetBytes(ptr, In[x].Length, ptrBuffer, len);
						Marshal.WriteIntPtr(list + (x * sizeof(IntPtr)), new IntPtr(ptrBuffer));
					}
				}
			}

			CMIgnoreList = ConstructIgnoreList(list, In.Count);
		}

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern ulong StringIndexOf(CMHandle CM, byte *In, byte *Contains, bool MatchRepeating, int StartIndex, CMListHandle IgnoreList);
		public unsafe (int Index, int Length) IndexOf(string In, string Contains, bool MatchRepeating, int StartIndex)
		{
			int inLen = Encoding.UTF8.GetByteCount(In);
			var inBuffer = stackalloc byte[inLen + 1];
			fixed (char *ptr = In) {
				Encoding.UTF8.GetBytes(ptr, In.Length, inBuffer, inLen);
			}

			int containsLen = Encoding.UTF8.GetByteCount(Contains);
			var containsBuffer = stackalloc byte[containsLen + 1];
			fixed (char* ptr = Contains) {
				Encoding.UTF8.GetBytes(ptr, Contains.Length, containsBuffer, containsLen);
			}

			var res = StringIndexOf(CMHandle, inBuffer, containsBuffer, MatchRepeating, StartIndex, CMIgnoreList);

			return ((int)(res & 0xFFFFFFFF), (int)(res >> 32));
		}

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern MAPPING_RESPONSE AddMapping(CMHandle CM, byte* Key, byte* Value, bool CheckValueDuplicate);
		public unsafe MAPPING_RESPONSE AddMapping(string Key, string Value, bool CheckValueDuplicate = true)
		{
			int keyLen = Encoding.UTF8.GetByteCount(Key);
			var keyBuffer = stackalloc byte[keyLen + 1];
			fixed (char* ptr = Key) {
				Encoding.UTF8.GetBytes(ptr, Key.Length, keyBuffer, keyLen);
			}

			int valLen = Encoding.UTF8.GetByteCount(Value);
			var valBuffer = stackalloc byte[valLen + 1];
			fixed (char* ptr = Value) {
				Encoding.UTF8.GetBytes(ptr, Value.Length, valBuffer, valLen);
			}

			return AddMapping(CMHandle, keyBuffer, valBuffer, CheckValueDuplicate);
		}

		[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static unsafe extern bool RemoveMapping(CMHandle CM, byte* Key, byte* Value);
		public unsafe bool RemoveMapping(string Key, string Value)
		{
			int keyLen = Encoding.UTF8.GetByteCount(Key);
			var keyBuffer = stackalloc byte[keyLen + 1];
			fixed (char* ptr = Key) {
				Encoding.UTF8.GetBytes(ptr, Key.Length, keyBuffer, keyLen);
			}

			int valLen = Encoding.UTF8.GetByteCount(Value);
			var valBuffer = stackalloc byte[valLen + 1];
			fixed (char* ptr = Value) {
				Encoding.UTF8.GetBytes(ptr, Value.Length, valBuffer, valLen);
			}

			return RemoveMapping(CMHandle, keyBuffer, valBuffer);
		}

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
