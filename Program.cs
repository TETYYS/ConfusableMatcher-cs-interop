#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ConfusableMatcherCSInterop;

public record struct CMOptions(bool MatchRepeating, ulong StartIndex, bool StartFromEnd, ulong TimeoutNs, bool MatchOnWordBoundary, IntPtr ContainsPosPointers)
{
	public static CMOptions Default => new(false, 0, false, 1000000, false, IntPtr.Zero);
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

	record struct CMMap(IntPtr Kv, uint Size);

	public enum CM_RETURN_STATUS
	{
		MATCH = 0,
		NO_MATCH = 1,
		TIMEOUT = 2,
		WORD_BOUNDARY_FAIL_START = 3,
		WORD_BOUNDARY_FAIL_END = 4
	}

	public record struct CMReturn(ulong Start, ulong Size, CM_RETURN_STATUS Status);

	public enum CM_DEBUG_FAILURE_REASON
	{
		NO_PATH = 0,
		NO_NEW_PATHS = 1,
		TIMEOUT = 2,
		WORD_BOUNDARY_FAIL_START = 3,
		WORD_BOUNDARY_FAIL_END = 4
	}

	public record struct CMDebugFailure(ulong InPos, ulong ContainsPos, CM_DEBUG_FAILURE_REASON Reason);
	public unsafe struct CMDebugFailures
	{
		public CMDebugFailure* Failures;
		public ulong Size;
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
			fixed (char* pIn = IgnoreList![x]) {
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
		fixed (byte* p = In) {
			var res = StringIndexOf(CMHandle, p, Contains, Options);
			return res;
		}
	}

	[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
	private static unsafe extern CMReturn StringIndexOfDebugFailures(IntPtr CM, byte *In, byte *Contains, CMOptions Options, CMDebugFailures *DebugOut);
	[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
	private static unsafe extern void FreeDebugFailures(CMDebugFailures* DebugFailures);

	unsafe string[] FormatDebugFailures(Span<byte> In, Span<byte> Contains, CMDebugFailures debugFailures)
	{
		var ret = new string[(int)debugFailures.Size];

		const char UNDERLINE = '̲';
		const int FRAGMENT_LEN = 3;

		for (ulong x = 0;x < debugFailures.Size;x++) {
			var failure = debugFailures.Failures[x];

			int inPos = (int)failure.InPos + 1;
			int containsPos = (int)failure.ContainsPos + 1;

			string inFragment, containsFragment;

			if (inPos > In.Length) {
				inPos--;
				inFragment = Encoding.UTF8.GetString(In[Math.Max(0, inPos - FRAGMENT_LEN)..inPos]) + " " + UNDERLINE;
			} else
				inFragment = Encoding.UTF8.GetString(In[Math.Max(0, inPos - FRAGMENT_LEN)..inPos]) + UNDERLINE + Encoding.UTF8.GetString(In[inPos..Math.Min(In.Length, inPos + FRAGMENT_LEN)]);

			if (containsPos > Contains.Length) {
				containsPos--;
				containsFragment = Encoding.UTF8.GetString(Contains[Math.Max(0, containsPos - FRAGMENT_LEN)..containsPos]) + " " + UNDERLINE;
			} else
				containsFragment = Encoding.UTF8.GetString(Contains[Math.Max(0, containsPos - FRAGMENT_LEN)..containsPos]) + UNDERLINE + Encoding.UTF8.GetString(Contains[containsPos..Math.Min(Contains.Length, containsPos + FRAGMENT_LEN)]);

			switch (failure.Reason) {
				case CM_DEBUG_FAILURE_REASON.NO_PATH:
					ret[x] = $"No path in input {inFragment} ({failure.InPos}) comparing {containsFragment} ({failure.ContainsPos})";
					break;
				case CM_DEBUG_FAILURE_REASON.NO_NEW_PATHS:
					ret[x] = $"No new paths in input {inFragment} ({failure.InPos}) comparing {containsFragment} ({failure.ContainsPos})";
					break;
				case CM_DEBUG_FAILURE_REASON.TIMEOUT:
					ret[x] = $"Timeout in input {inFragment} ({failure.InPos}) comparing {containsFragment} ({failure.ContainsPos})";
					break;
				case CM_DEBUG_FAILURE_REASON.WORD_BOUNDARY_FAIL_START:
					ret[x] = $"Word boundary failure at start of the match in input {inFragment} ({failure.InPos}) comparing {containsFragment} ({failure.ContainsPos})";
					break;
				case CM_DEBUG_FAILURE_REASON.WORD_BOUNDARY_FAIL_END:
					ret[x] = $"Word boundary failure at end of the match in input {inFragment} ({failure.InPos}) comparing {containsFragment} ({failure.ContainsPos})";
					break;
			}
		}

		return ret;
	}

	public (CMReturn Status, string[] Failures) IndexOfDebugFailures(ReadOnlySpan<char> In, ReadOnlySpan<char> Contains, CMOptions Options)
	{
		if (Options.StartIndex != 0) {
			// Convert StartIndex in UTF8 terms
			var startSkip = In[..(int)Options.StartIndex];
			Options.StartIndex = (nuint)Encoding.UTF8.GetByteCount(startSkip);
		}

		int inUtf8len = Encoding.UTF8.GetMaxByteCount(In.Length);
		Span<byte> utf8In = stackalloc byte[inUtf8len + 1];
		var bytes = Encoding.UTF8.GetBytes(In, utf8In);
		utf8In = utf8In[..bytes];

		int containsUtf8len = Encoding.UTF8.GetMaxByteCount(Contains.Length);
		Span<byte> utf8Contains = stackalloc byte[containsUtf8len + 1];
		bytes = Encoding.UTF8.GetBytes(Contains, utf8Contains);
		utf8Contains = utf8Contains[..bytes];

		var (status, failures) = IndexOfDebugFailures(utf8In, utf8Contains, Options);

		var start = utf8In[..(int)status.Start];
		var matchedPart = utf8In[(int)status.Start..((int)status.Start+(int)status.Size)];

		var cmRet = new CMReturn((ulong)Encoding.UTF8.GetCharCount(start), (ulong)Encoding.UTF8.GetCharCount(matchedPart), status.Status);
		return (cmRet, failures);
	}

	public unsafe (CMReturn Status, string[] Failures) IndexOfDebugFailures(Span<byte> In, Span<byte> Contains, CMOptions Options)
	{
		CMDebugFailures debugOut;

		fixed (byte* pIn = In) {
			fixed (byte* pContains = Contains) {
				var res = StringIndexOfDebugFailures(CMHandle, pIn, pContains, Options, &debugOut);
				var failures = FormatDebugFailures(In, Contains, debugOut);
				FreeDebugFailures(&debugOut);
				return (res, failures);
			}
		}
	}

	public unsafe (CMReturn Status, CMDebugFailure[] Failures) IndexOfDebugFailuresEx(Span<byte> In, Span<byte> Contains, CMOptions Options)
	{
		CMDebugFailures debugOut;

		fixed (byte* pIn = In) {
			fixed (byte* pContains = Contains) {
				var res = StringIndexOfDebugFailures(CMHandle, pIn, pContains, Options, &debugOut);
				var failures = new CMDebugFailure[debugOut.Size];
				for (ulong x = 0;x < debugOut.Size;x++) {
					failures[x] = debugOut.Failures[x];
				}
				FreeDebugFailures(&debugOut);
				return (res, failures);
			}
		}
	}

	public unsafe (CMReturn Status, CMDebugFailure[] Failures) IndexOfDebugFailuresEx(ReadOnlySpan<char> In, ReadOnlySpan<char> Contains, CMOptions Options)
	{
		if (Options.StartIndex != 0) {
			// Convert StartIndex in UTF8 terms
			var startSkip = In[..(int)Options.StartIndex];
			Options.StartIndex = (nuint)Encoding.UTF8.GetByteCount(startSkip);
		}

		int inUtf8len = Encoding.UTF8.GetMaxByteCount(In.Length);
		Span<byte> utf8In = stackalloc byte[inUtf8len + 1];
		var bytes = Encoding.UTF8.GetBytes(In, utf8In);
		utf8In = utf8In[..bytes];

		int containsUtf8len = Encoding.UTF8.GetMaxByteCount(Contains.Length);
		Span<byte> utf8Contains = stackalloc byte[containsUtf8len + 1];
		bytes = Encoding.UTF8.GetBytes(Contains, utf8Contains);
		utf8Contains = utf8Contains[..bytes];

		var (status, failures) = IndexOfDebugFailuresEx(utf8In, utf8Contains, Options);

		var start = utf8In[..(int)status.Start];
		var matchedPart = utf8In[(int)status.Start..((int)status.Start + (int)status.Size)];

		var cmRet = new CMReturn((ulong)Encoding.UTF8.GetCharCount(start), (ulong)Encoding.UTF8.GetCharCount(matchedPart), status.Status);
		return (cmRet, failures);
	}

	[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
	private static unsafe extern uint GetKeyMappings(IntPtr CM, [MarshalAs(UnmanagedType.LPUTF8Str)] string In, byte* Output, uint OutputSize);
	public unsafe List<string> GetKeyMappings(string In)
	{
		if (In == null) {
			throw new ArgumentNullException(nameof(In));
		}

		static List<string> Get(byte* pBuffer, int Size)
		{
			var ret = new List<string>(Size);
			for (var x = 0;x < Size;x++) {
				var arrPtr = (byte**)pBuffer;

				int strSz = 0;
				while (arrPtr[x][++strSz] != 0x00);

				var copy = Encoding.UTF8.GetString(arrPtr[x], strSz);
				ret.Add(copy);
			}

			return ret;
		}

		byte* outputPtr = stackalloc byte[IntPtr.Size * 32];

		var sz = GetKeyMappings(CMHandle, In, outputPtr, 32);

		if (sz > 32) {
			fixed(byte* pBuffer = new byte[IntPtr.Size * sz]) {
				_ = GetKeyMappings(CMHandle, In, pBuffer, sz);
				return Get(pBuffer, (int)sz);
			}
		}

		return Get(outputPtr, (int)sz);
	}

	[DllImport("ConfusableMatcher", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr ComputeStringPosPointers(IntPtr CM, [MarshalAs(UnmanagedType.LPUTF8Str)] string Contains);

	public IntPtr ComputeStringPosPointers(string Contains)
	{
		return ComputeStringPosPointers(CMHandle, Contains);
	}

	[DllImport("ConfusableMatcher", EntryPoint = "FreeStringPosPointers", CallingConvention = CallingConvention.Cdecl)]
	private static extern void FreeStringPosPtr(IntPtr StringPosPointers);

	public void FreeStringPosPointers(IntPtr StringPosPointers)
	{
		FreeStringPosPtr(StringPosPointers);
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