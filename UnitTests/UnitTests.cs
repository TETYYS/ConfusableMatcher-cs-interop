using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ConfusableMatcherCSInterop;
using Xunit;

using static ConfusableMatcherCSInterop.ConfusableMatcher;

namespace UnitTests
{
	public class UnitTests
	{
		void AssertMatch(CMReturn In, int Start, int Size, CM_RETURN_STATUS Status = CM_RETURN_STATUS.MATCH)
		{
			Assert.Equal(Status, In.Status);
			Assert.Equal(Start, (int)In.Start);
			Assert.Equal(Size, (int)In.Size);
		}

		void AssertMatchMulti(CMReturn In, int[] Start, int[] Size, CM_RETURN_STATUS Status = CM_RETURN_STATUS.MATCH)
		{
			Assert.Equal(Status, In.Status);
			Assert.Equal(Start.Length, Size.Length);

			bool ok = false;
			for (var x = 0;x < Start.Length;x++) {
				if (Start[x] == (int)In.Start && Size[x] == (int)In.Size) {
					ok = true;
					break;
				}
			}

			Assert.True(ok);
		}

		void AssertNoMatch(CMReturn In)
		{
			Assert.Equal(CM_RETURN_STATUS.NO_MATCH, In.Status);
		}

		[Fact]
		public void Test1()
		{
			var map = new List<(string Key, string Value)>();

			map.Add(("N", "T"));
			map.Add(("I", "E"));
			map.Add(("C", "S"));
			map.Add(("E", "T"));

			var matcher = new ConfusableMatcher(map, null);
			var res = matcher.IndexOf("TEST", "NICE", CMOptions.Default);
			AssertMatch(res, 0, 4);
		}

		[Fact]
		void Test2()
		{
			var map = new List<(string Key, string Value)>();

			map.Add(("V", "VA"));
			map.Add(("V", "VO"));

			var matcher = new ConfusableMatcher(map, null);
			var res = matcher.IndexOf("VV", "VAVOVAVO", CMOptions.Default);
			AssertNoMatch(res);
			res = matcher.IndexOf("VAVOVAVO", "VV", CMOptions.Default);
			AssertMatchMulti(res, new[] { 0, 0 }, new[] { 3, 4 });
			CMOptions opts = CMOptions.Default;
			opts.StartIndex = 4;
			res = matcher.IndexOf("VAVOVAVO", "VV", opts);
			AssertMatchMulti(res, new[] { 4, 4 }, new[] { 3, 4 });
			opts.StartIndex = 2;
			res = matcher.IndexOf("VAVOVAVO", "VV", opts);
			AssertMatchMulti(res, new[] { 2, 2 }, new[] { 3, 4 });
			opts.StartIndex = 3;
			res = matcher.IndexOf("VAVOVAVO", "VV", opts);
			AssertMatchMulti(res, new[] { 4, 4 }, new[] { 3, 4 });
		}

		[Fact]
		void Test3()
		{
			var map = new List<(string Key, string Value)>();

			map.Add(("A", "\x02\x03"));
			map.Add(("B", "\xFA\xFF"));

			var matcher = new ConfusableMatcher(map, null);
			var res = matcher.IndexOf("\x02\x03\xFA\xFF", "AB", CMOptions.Default);
			AssertMatch(res, 0, 4);
		}

		[Fact]
		void Test4()
		{
			var map = new List<(string Key, string Value)>();

			map.Add(("S", "$"));
			map.Add(("D", "[)"));

			var matcher = new ConfusableMatcher(map, new[] { "_", " " });
			var opts = CMOptions.Default;
			opts.MatchRepeating = true;

			var res = matcher.IndexOf("A__ _ $$$[)D", "ASD", opts);
			AssertMatch(res, 0, 11);
		}

		[Fact]
		void Test5()
		{
			var map = new List<(string Key, string Value)>();

			map.Add(("N", "/\\/"));
			map.Add(("N", "/\\"));
			map.Add(("I", "/"));

			var matcher = new ConfusableMatcher(map, null);
			var res = matcher.IndexOf("/\\/CE", "NICE", CMOptions.Default);
			AssertMatch(res, 0, 5);
		}

		[Fact]
		void Test6()
		{
			var map = new List<(string Key, string Value)>();

			map.Add(("N", "/\\/"));
			map.Add(("V", "\\/"));
			map.Add(("I", "/"));

			var opts = CMOptions.Default;
			opts.MatchRepeating = true;
			var matcher = new ConfusableMatcher(map, null);
			var res = matcher.IndexOf("I/\\/AM", "INAN", opts);
			AssertNoMatch(res);
			res = matcher.IndexOf("I/\\/AM", "INAM", opts);
			AssertMatch(res, 0, 6);
			res = matcher.IndexOf("I/\\/AM", "IIVAM", opts);
			AssertMatch(res, 0, 6);
		}

		List<(string Key, string Value)> GetDefaultMap()
		{
			var map = new List<(string Key, string Value)>();

			map.Add(("N", "/[()[]]/"));
			map.Add(("N", "\U000000f1"));
			map.Add(("N", "|\\|"));
			map.Add(("N", "\U00000245\U0000002f"));
			map.Add(("N", "/IJ"));
			map.Add(("N", "/|/"));

			var ns = new[] { "\U000004c5", "\U000003a0", "\U00000418", "\U0001d427", "\U0001d45b", "\U0001d48f", "\U0001d4c3", "\U0001d4f7", "\U0001d52b", "\U0001d55f", "\U0001d593", "\U0001d5c7", "\U0001d5fb", "\U0001d62f", "\U0001d663", "\U0001d697", "\U00000578", "\U0000057c", "\U0000ff2e", "\U00002115", "\U0001d40d", "\U0001d441", "\U0001d475", "\U0001d4a9", "\U0001d4dd", "\U0001d511", "\U0001d579", "\U0001d5ad", "\U0001d5e1", "\U0001d615", "\U0001d649", "\U0001d67d", "\U0000039d", "\U0001d6b4", "\U0001d6ee", "\U0001d728", "\U0001d762", "\U0001d79c", "\U0000a4e0", "\U00000143", "\U00000145", "\U00000147", "\U0000014b", "\U0000019d", "\U000001f8", "\U00000220", "\U0000039d", "\U00001e44", "\U00001e46", "\U00001e48", "\U00001e4a", "\U000020a6", "\U00001f20", "\U00001f21", "\U00001f22", "\U00001f23", "\U00001f24", "\U00001f25", "\U00001f26", "\U00001f27", "\U00001f74", "\U00001f75", "\U00001f90", "\U00001f91", "\U00001f92", "\U00001f93", "\U00001f94", "\U00001f95", "\U00001f96", "\U00001f97", "\U00001fc2", "\U00001fc3", "\U00001fc4", "\U00001fc6", "\U00001fc7", "\U000000f1", "\U00000144", "\U00000146", "\U00000148", "\U00000149", "\U0000014a", "\U0000019e", "\U000001f9", "\U00000235", "\U00000272", "\U00000273", "\U00000274", "\U00001d70", "\U00001d87", "\U00001e45", "\U00001e47", "\U00001e49", "\U00001e4b" };
			var @is = new[] { "\U00001ec8", "\U00000079", "\U00000069", "\U00000031", "\U0000007c", "\U0000006c", "\U0000006a", "\U00000021", "\U0000002f", "\U0000005c\U0000005c", "\U0000ff49", "\U000000a1", "\U00002170", "\U00002139", "\U00002148", "\U0001d422", "\U0001d456", "\U0001d48a", "\U0001d4be", "\U0001d4f2", "\U0001d526", "\U0001d55a", "\U0001d58e", "\U0001d5c2", "\U0001d5f6", "\U0001d62a", "\U0001d65e", "\U0001d692", "\U00000131", "\U0001d6a4", "\U0000026a", "\U00000269", "\U000003b9", "\U00001fbe", "\U0000037a", "\U0001d6ca", "\U0001d704", "\U0001d73e", "\U0001d778", "\U0001d7b2", "\U00000456", "\U000024be", "\U0000a647", "\U000004cf", "\U0000ab75", "\U000013a5", "\U00000263", "\U00001d8c", "\U0000ff59", "\U0001d432", "\U0001d466", "\U0001d49a", "\U0001d4ce", "\U0001d502", "\U0001d536", "\U0001d56a", "\U0001d59e", "\U0001d5d2", "\U0001d606", "\U0001d63a", "\U0001d66e", "\U0001d6a2", "\U0000028f", "\U00001eff", "\U0000ab5a", "\U000003b3", "\U0000213d", "\U0001d6c4", "\U0001d6fe", "\U0001d738", "\U0001d772", "\U0001d7ac", "\U00000443", "\U000004af", "\U000010e7", "\U0000ff39", "\U0001d418", "\U0001d44c", "\U0001d480", "\U0001d4b4", "\U0001d4e8", "\U0001d51c", "\U0001d550", "\U0001d584", "\U0001d5b8", "\U0001d5ec", "\U0001d620", "\U0001d654", "\U0001d688", "\U000003a5", "\U000003d2", "\U0001d6bc", "\U0001d6f6", "\U0001d730", "\U0001d76a", "\U0001d7a4", "\U00002ca8", "\U00000423", "\U000004ae", "\U000013a9", "\U000013bd", "\U0000a4ec", "\U00000176", "\U00000178", "\U000001b3", "\U00000232", "\U0000024e", "\U0000028f", "\U00001e8e", "\U00001ef2", "\U00001ef4", "\U00001ef6", "\U00001ef8", "\U0000ff39", "\U000000cc", "\U000000cd", "\U000000ce", "\U000000cf", "\U00000128", "\U0000012a", "\U0000012c", "\U0000012e", "\U00000130", "\U00000196", "\U00000197", "\U000001cf", "\U00000208", "\U0000020a", "\U0000026a", "\U0000038a", "\U00000390", "\U00000399", "\U000003aa", "\U00000406", "\U0000040d", "\U00000418", "\U00000419", "\U000004e2", "\U000004e4", "\U00001e2c", "\U00001e2e", "\U00001ec8", "\U00001eca", "\U00001fd8", "\U00001fd9", "\U00002160", "\U0000ff29", "\U000030a7", "\U000030a8", "\U0000ff6a", "\U0000ff74", "\U000000ec", "\U000000ed", "\U000000ee", "\U000000ef", "\U00000129", "\U0000012b", "\U0000012d", "\U0000012f", "\U00000131", "\U000001d0", "\U00000209", "\U0000020b", "\U00000268", "\U00000269", "\U00000365", "\U000003af", "\U000003ca", "\U00000438", "\U00000439", "\U00000456", "\U0000045d", "\U000004e3", "\U000004e5", "\U00001e2d", "\U00001e2f", "\U00001ec9", "\U00001ecb", "\U00001f30", "\U00001f31", "\U00001f32", "\U00001f33", "\U00001f34", "\U00001f35", "\U00001f36", "\U00001f37", "\U00001f76", "\U00001f77", "\U00001fbe", "\U00001fd0", "\U00001fd1", "\U00001fd2", "\U00001fd3", "\U00001fd6", "\U00001fd7", "\U0000ff49", "\U00001d85", "\U00001e37", "\U00001e39", "\U00001e3b", "\U00001e3d", "\U000000fd", "\U000000ff", "\U00000177", "\U000001b4", "\U00000233", "\U0000024f", "\U0000028e", "\U000002b8", "\U00001e8f", "\U00001e99", "\U00001ef3", "\U00001ef5", "\U00001ef7", "\U00001ef9", "\U0000ff59" };
			var gs = new[] { "\U0000006b", "\U00000067", "\U00000071", "\U00000034", "\U00000036", "\U00000039", "\U0000011f", "\U00000d6b", "\U0000ff47", "\U0000210a", "\U0001d420", "\U0001d454", "\U0001d488", "\U0001d4f0", "\U0001d524", "\U0001d558", "\U0001d58c", "\U0001d5c0", "\U0001d5f4", "\U0001d628", "\U0001d65c", "\U0001d690", "\U00000261", "\U00001d83", "\U0000018d", "\U00000581", "\U0001d406", "\U0001d43a", "\U0001d46e", "\U0001d4a2", "\U0001d4d6", "\U0001d50a", "\U0001d53e", "\U0001d572", "\U0001d5a6", "\U0001d5da", "\U00004e48", "\U0001d60e", "\U0001d642", "\U0001d676", "\U0000050c", "\U000013c0", "\U000013f3", "\U0000a4d6", "\U0000011c", "\U0000011e", "\U00000120", "\U00000122", "\U00000193", "\U000001e4", "\U000001e6", "\U000001f4", "\U0000029b", "\U00000393", "\U00000413", "\U00001e20", "\U0000ff27", "\U000013b6", "\U0000011d", "\U0000011f", "\U00000121", "\U00000123", "\U000001e5", "\U000001e7", "\U000001f5", "\U00000260", "\U00000261", "\U00000262", "\U00000040" };
			var es = new[] { "\U00001ec0", "\U000003a3", "\U0000039e", "\U00000065", "\U00000033", "\U00000075", "\U0000212e", "\U0000ff45", "\U0000212f", "\U00002147", "\U0001d41e", "\U0001d452", "\U0001d486", "\U0001d4ee", "\U0001d522", "\U0001d556", "\U0001d58a", "\U0001d5be", "\U0001d5f2", "\U0001d626", "\U0001d65a", "\U0001d68e", "\U0000ab32", "\U00000435", "\U000004bd", "\U000022ff", "\U0000ff25", "\U00002130", "\U0001d404", "\U0001d438", "\U0001d46c", "\U0001d4d4", "\U0001d508", "\U0001d53c", "\U0001d570", "\U0001d5a4", "\U0001d5d8", "\U0001d60c", "\U0001d640", "\U0001d674", "\U00000395", "\U0001d6ac", "\U0001d6e6", "\U0001d720", "\U0001d75a", "\U0001d794", "\U00000415", "\U00002d39", "\U000013ac", "\U0000a4f0", "\U000000c8", "\U000000c9", "\U000000ca", "\U000000cb", "\U00000112", "\U00000114", "\U00000116", "\U00000118", "\U0000011a", "\U0000018e", "\U00000190", "\U00000204", "\U00000206", "\U00000228", "\U00000246", "\U00000388", "\U0000042d", "\U000004ec", "\U00001e14", "\U00001e16", "\U00001e18", "\U00001e1a", "\U00001e1c", "\U00001eb8", "\U00001eba", "\U00001ebc", "\U00001ebe", "\U00001ec0", "\U00001ec2", "\U00001ec4", "\U00001ec6", "\U00001f18", "\U00001f19", "\U00001f1a", "\U00001f1b", "\U00001f1c", "\U00001f1d", "\U00001fc8", "\U00001fc9", "\U000000e8", "\U000000e9", "\U000000ea", "\U000000eb", "\U00000113", "\U00000115", "\U00000117", "\U00000119", "\U0000011b", "\U0000018f", "\U00000205", "\U00000207", "\U00000229", "\U00000247", "\U00000258", "\U0000025b", "\U0000025c", "\U0000025d", "\U0000025e", "\U00000364", "\U000003ad", "\U000003b5", "\U00000435", "\U0000044d", "\U000004ed", "\U00001e15", "\U00001e17", "\U00001e19", "\U00001e1b", "\U00001e1d", "\U00001eb9", "\U00001ebb", "\U00001ebd", "\U00001ebf", "\U00001ec1", "\U00001ec3", "\U00001ec5", "\U00001ec7", "\U00001f10", "\U00001f11", "\U00001f12", "\U00001f13", "\U00001f14", "\U00001f15", "\U00001f72", "\U00001f73" };
			var rs = new[] { "\U00000403", "\U0000042f", "\U00000072", "\U0001d42b", "\U0001d45f", "\U0001d493", "\U0001d4c7", "\U0001d4fb", "\U0001d52f", "\U0001d563", "\U0001d597", "\U0001d5cb", "\U0001d5ff", "\U0001d633", "\U0001d667", "\U0001d69b", "\U0000ab47", "\U0000ab48", "\U00001d26", "\U00002c85", "\U00000433", "\U0000ab81", "\U0000211b", "\U0000211c", "\U0000211d", "\U0001d411", "\U0001d445", "\U0001d479", "\U0001d4e1", "\U0001d57d", "\U0001d5b1", "\U0001d5e5", "\U0001d619", "\U0001d64d", "\U0001d681", "\U000001a6", "\U000013a1", "\U000013d2", "\U000104b4", "\U00001587", "\U0000a4e3", "\U00000154", "\U00000156", "\U00000158", "\U00000210", "\U00000212", "\U0000024c", "\U00000280", "\U00000281", "\U00001e58", "\U00001e5a", "\U00001e5c", "\U00001e5e", "\U00002c64", "\U0000ff32", "\U000013a1", "\U00000155", "\U00000157", "\U00000159", "\U00000211", "\U00000213", "\U0000024d", "\U00000279", "\U0000027a", "\U0000027b", "\U0000027c", "\U0000027d", "\U000016b1", "\U00001875", "\U00001d72", "\U00001d73", "\U00001d89", "\U00001e59", "\U00001e5b", "\U00001e5d", "\U00001e5f", "\U0000ff52" };

			var s = new[] { ns, @is, gs, es, rs };

			for (var x = 0;x < 5;x++) {
				foreach (var chr in s[x]) {
					switch (x) {
						case 0:
							map.Add(("N", chr));
							break;
						case 1:
							map.Add(("I", chr));
							break;
						case 2:
							map.Add(("G", chr));
							break;
						case 3:
							map.Add(("E", chr));
							break;
						case 4:
							map.Add(("R", chr));
							break;
					}
				}
			}

			return map;
		}

		[Fact]
		void Test7()
		{
			var map = GetDefaultMap();

			var @in = "AAAAAAAAASSAFSAFNFNFNISFNSIFSIFJSDFUDSHF ASUF/|/__/|/___%/|/%I%%/|//|/%%%%%NNNN/|/NN__/|/N__𝘪G___%____$__G__𝓰𝘦Ѓ";

			var opts = CMOptions.Default;
			opts.MatchRepeating = true;

			var matcher = new ConfusableMatcher(map, new[] { "_", "%", "$" });
			var res = matcher.IndexOf(@in, "NIGGER", opts);
			AssertMatchMulti(res, new[] { 64, 89 }, new[] { 50, 25 });
		}

		[Theory]
		[InlineData("\U00000105", "A", 0, 1)]
		[InlineData("\U0000ab31", "A", 0, 1)]
		[InlineData("\U00001d43", "A", 0, 1)]
		[InlineData("abc \U000000e5 def", "ABC A DEF", 0, 9)]
		[InlineData("\U000002e2\U00001d50\U00001d52\U000002e1 \U0000207f\U00001d43\U00001d57\U00001da6\U00001d52\U0000207f", "SMOL NATION", 0, 11)]
		[InlineData("\U0000041d\U00000438\U00000433", "NIG", 0, 3)]
		[InlineData("\U0001f1fa\U0001f1e6XD", "UAXD", 0, 6)]
		[InlineData("\U0001f193 ICE", "FREE ICE", 0, 6)]
		[InlineData("chocolate \U0001F1F3\U0001F1EEb", "CHOCOLATE NIB", 0, 15)]
		[InlineData("\U0001f171lueberry", "BLUEBERRY", 0, 10)]
		[InlineData("\U0000249d", "B", 0, 1)]
		[InlineData("\U000000fc \U000000dc \U000000f6 \U000000d6 \U000000e4 \U000000c4", "U U O O A A", 0, 11)]
		[InlineData("\U00001d2d", "AE", 0, 1)]
		[InlineData("\U0000249c \U0000249d \U0000249e \U0000249f \U000024a0 \U000024a1 \U000024a2 \U000024a3 \U000024a4 \U000024a5 \U000024a6 \U000024a7 \U000024a8 \U000024a9 \U000024aa \U000024ab \U000024ac \U000024ad \U000024ae \U000024af \U000024b0 \U000024b1 \U000024b2 \U000024b3 \U000024b4", "A B C D E F G H I J K L M N O P Q R S T U V W X Y", 0, 49)]
		[InlineData("\U000024cf\U000024d0\U000024d1\U000024d2\U000024d3\U000024d4\U000024d5\U000024d6\U000024d7\U000024d8\U000024d9\U000024da\U000024db\U000024dc\U000024dd\U000024de\U000024df\U000024e0\U000024e1\U000024e2\U000024e3\U000024e4\U000024e5\U000024e6\U000024e7\U000024e8\U000024e9\U000024ea", "ZABCDEFGHIJKLMNOPQRSTUVWXYZ0", 0, 28)]
		[InlineData("\U0001d552\U0001d553\U0001d554\U0001d555\U0001d556\U0001d557\U0001d558\U0001d559\U0001d55a\U0001d55b\U0001d55c\U0001d55d\U0001d55e\U0001d55f\U0001d560\U0001d561\U0001d562\U0001d563\U0001d564\U0001d565\U0001d566\U0001d567\U0001d568\U0001d569\U0001d56a\U0001d56b", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0, 52)]
		[InlineData("\U0001f130\U0001f131\U0001f132\U0001f133\U0001f134\U0001f135\U0001f136\U0001f137\U0001f138\U0001f139\U0001f13a\U0001f13b\U0001f13c\U0001f13d\U0001f13e\U0001f13f\U0001f140\U0001f141\U0001f142\U0001f143\U0001f144\U0001f145\U0001f146\U0001f147\U0001f148\U0001f149", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0, 52)]
		[InlineData("\U000020b3\U00000e3f\U000020b5\U00000110\U00000246\U000020a3\U000020b2\U00002c67\U00000142J\U000020ad\U00002c60\U000020a5\U000020a6\U000000d8\U000020b1Q\U00002c64\U000020b4\U000020ae\U00000244V\U000020a9\U000004fe\U0000024e\U00002c6b", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0, 26)]
		[InlineData("\U0001d586\U0001d587\U0001d588\U0001d589\U0001d58a\U0001d58b\U0001d58c\U0001d58d\U0001d58e\U0001d58f\U0001d590\U0001d591\U0001d592\U0001d593\U0001d594\U0001d595\U0001d596\U0001d597\U0001d598\U0001d599\U0001d59a\U0001d59b\U0001d59c\U0001d59d\U0001d59e\U0001d59f", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0, 52)]
		[InlineData("\U0001f170\U0001f171\U0001f172\U0001f173\U0001f174\U0001f175\U0001f176\U0001f177\U0001f178\U0001f179\U0001f17a\U0001f17b\U0001f17c\U0001f17d\U0001f17e\U0001f17f\U0001f180\U0001f181\U0001f182\U0001f183\U0001f184\U0001f185\U0001f186\U0001f187\U0001f188\U0001f189", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0, 52)]
		void LidlNormalizerTests(string In, string Contains, int ExpectedIndex, int ExpectedLength)
		{
			var map = GetDefaultMap();

			// Additional test data
			var keys = new[] {
				"A", "A", "A", "A", "B", "U", "U", "O", "O", "A", "A",
				"A", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y",
				"Z", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "0",
				"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
				"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
				"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
				"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
				"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
				"U", "A", " ", "S", "M", "O", "L", "N", "A", "T", "I", "O", "N", "N", "I", "G", "N", "I", "FREE", "AE"
			};
			var vals = new[] {
				"\U00000105", "\U0000ab31", "\U00001d43", "\U000000e5", "\U0000249d", "\U000000fc", "\U000000dc", "\U000000f6", "\U000000d6", "\U000000e4", "\U000000c4", "\U0000249c", "\U0000249e", "\U0000249f", "\U000024a0", "\U000024a1", "\U000024a2", "\U000024a3", "\U000024a4", "\U000024a5", "\U000024a6", "\U000024a7", "\U000024a8", "\U000024a9", "\U000024aa", "\U000024ab", "\U000024ac", "\U000024ad", "\U000024ae", "\U000024af", "\U000024b0", "\U000024b1", "\U000024b2", "\U000024b3", "\U000024b4", "\U000024cf", "\U000024d0", "\U000024d1", "\U000024d2", "\U000024d3", "\U000024d4", "\U000024d5", "\U000024d6", "\U000024d7", "\U000024d8", "\U000024d9", "\U000024da", "\U000024db", "\U000024dc", "\U000024dd", "\U000024de", "\U000024df", "\U000024e0", "\U000024e1", "\U000024e2", "\U000024e3", "\U000024e4", "\U000024e5", "\U000024e6", "\U000024e7", "\U000024e8", "\U000024e9", "\U000024ea", "\U0001d552", "\U0001d553", "\U0001d554", "\U0001d555", "\U0001d556", "\U0001d557", "\U0001d558", "\U0001d559", "\U0001d55a", "\U0001d55b", "\U0001d55c", "\U0001d55d", "\U0001d55e", "\U0001d55f", "\U0001d560", "\U0001d561", "\U0001d562", "\U0001d563", "\U0001d564", "\U0001d565", "\U0001d566", "\U0001d567", "\U0001d568", "\U0001d569", "\U0001d56a", "\U0001d56b", "\U0001f130", "\U0001f131", "\U0001f132", "\U0001f133", "\U0001f134", "\U0001f135", "\U0001f136", "\U0001f137", "\U0001f138", "\U0001f139", "\U0001f13a", "\U0001f13b", "\U0001f13c", "\U0001f13d", "\U0001f13e", "\U0001f13f", "\U0001f140", "\U0001f141", "\U0001f142", "\U0001f143", "\U0001f144", "\U0001f145", "\U0001f146", "\U0001f147", "\U0001f148", "\U0001f149", "\U000020b3", "\U00000e3f", "\U000020b5", "\U00000110", "\U00000246", "\U000020a3", "\U000020b2", "\U00002c67", "\U00000142", "\U0000004a", "\U000020ad", "\U00002c60", "\U000020a5", "\U000020a6", "\U000000d8", "\U000020b1", "\U00000051", "\U00002c64", "\U000020b4", "\U000020ae", "\U00000244", "\U00000056", "\U000020a9", "\U000004fe", "\U0000024e", "\U00002c6b", "\U0001d586", "\U0001d587", "\U0001d588", "\U0001d589", "\U0001d58a", "\U0001d58b", "\U0001d58c", "\U0001d58d", "\U0001d58e", "\U0001d58f", "\U0001d590", "\U0001d591", "\U0001d592", "\U0001d593", "\U0001d594", "\U0001d595", "\U0001d596", "\U0001d597", "\U0001d598", "\U0001d599", "\U0001d59a", "\U0001d59b", "\U0001d59c", "\U0001d59d", "\U0001d59e", "\U0001d59f", "\U0001f170", "\U0001f171", "\U0001f172", "\U0001f173", "\U0001f174", "\U0001f175", "\U0001f176", "\U0001f177", "\U0001f178", "\U0001f179", "\U0001f17a", "\U0001f17b", "\U0001f17c", "\U0001f17d", "\U0001f17e", "\U0001f17f", "\U0001f180", "\U0001f181", "\U0001f182", "\U0001f183", "\U0001f184", "\U0001f185", "\U0001f186", "\U0001f187", "\U0001f188", "\U0001f189", "\U0001f1fa", "\U0001f1e6", " ", "\U000002e2", "\U00001d50", "\U00001d52", "\U000002e1", "\U0000207f", "\U00001d43", "\U00001d57", "\U00001da6", "\U00001d52", "\U0000207f", "\U0000041d", "\U00000438", "\U00000433", "\U0001F1F3", "\U0001F1EE", "\U0001f193", "\U00001d2d"
			};

			for (var x = 0;x < keys.Length;x++)
				map.Add((keys[x], vals[x]));

			var opts = CMOptions.Default;
			opts.MatchRepeating = true;
			var matcher = new ConfusableMatcher(map, null);

			var res = matcher.IndexOf(In, Contains, opts);
			AssertMatch(res, ExpectedIndex, ExpectedLength);
		}

		[Fact]
		void Test8()
		{
			var map = new List<(string Key, string Value)>();

			var matcher = new ConfusableMatcher(map, new[] { "\U00000332", "\U00000305", "[", "]" });
			var res = matcher.IndexOf(
				"[̲̅a̲̅][̲̅b̲̅][̲̅c̲̅][̲̅d̲̅][̲̅e̲̅][̲̅f̲̅][̲̅g̲̅][̲̅h̲̅][̲̅i̲̅][̲̅j̲̅][̲̅k̲̅][̲̅l̲̅][̲̅m̲̅][̲̅n̲̅][̲̅o̲̅][̲̅p̲̅][̲̅q̲̅][̲̅r̲̅][̲̅s̲̅][̲̅t̲̅][̲̅u̲̅][̲̅v̲̅][̲̅w̲̅][̲̅x̲̅][̲̅y̲̅][̲̅z̲̅][̲̅0̲̅][̲̅1̲̅][̲̅2̲̅][̲̅3̲̅][̲̅4̲̅][̲̅5̲̅][̲̅6̲̅][̲̅7̲̅][̲̅8̲̅][̲̅9̲̅][̲̅0̲̅]",
				"ABCDEFGHIJKLMNOPQRSTUVWXYZ01234567890",
				CMOptions.Default);
			AssertMatch(res, 3, 253);
		}

		[Fact]
		void Test9()
		{
			var map = new List<(string Key, string Value)>();

			map.Add(("B", "A"));
			map.Add(("B", "AB"));
			map.Add(("B", "ABC"));
			map.Add(("B", "ABCD"));
			map.Add(("B", "ABCDE"));
			map.Add(("B", "ABCDEF"));
			map.Add(("B", "ABCDEFG"));
			map.Add(("B", "ABCDEFGH"));
			map.Add(("B", "ABCDEFGHI"));
			map.Add(("B", "ABCDEFGHIJ"));
			map.Add(("B", "ABCDEFGHIJK"));
			map.Add(("B", "ABCDEFGHIJKL"));
			map.Add(("B", "ABCDEFGHIJKLM"));
			map.Add(("B", "ABCDEFGHIJKLMN"));
			map.Add(("B", "ABCDEFGHIJKLMNO"));
			map.Add(("B", "ABCDEFGHIJKLMNOP"));
			map.Add(("B", "ABCDEFGHIJKLMNOPQ"));
			map.Add(("B", "ABCDEFGHIJKLMNOPQR"));
			map.Add(("B", "ABCDEFGHIJKLMNOPQRS"));

			var matcher = new ConfusableMatcher(map, null);

			var res = matcher.IndexOf(
				"ABCDEFGHIJKLMNOPQRS",
				"B",
				CMOptions.Default);
			AssertMatchMulti(res, new[] { 0, 0 }, new[] { 1, 1 });

			map.Remove(("B", "ABCDEFGHIJKLMNOP"));
			map.Add(("B", "P"));
			map.Add(("B", "PQ"));
			map.Add(("B", "PQR"));
			map.Add(("B", "PQRS"));
			map.Add(("B", "PQRST"));
			map.Add(("B", "PQRSTU"));
			map.Add(("B", "PQRSTUV"));
			map.Add(("B", "PQRSTUVW"));
			map.Add(("B", "PQRSTUVWX"));
			map.Add(("B", "PQRSTUVWXY"));
			map.Add(("B", "PQRSTUVWXYZ"));
			map.Add(("B", "PQRSTUVWXYZ0"));
			map.Add(("B", "PQRSTUVWXYZ01"));
			map.Add(("B", "PQRSTUVWXYZ012"));
			map.Add(("B", "PQRSTUVWXYZ0123"));
			map.Add(("B", "PQRSTUVWXYZ01234"));
			map.Add(("B", "PQRSTUVWXYZ012345"));
			map.Add(("B", "PQRSTUVWXYZ0123456"));
			map.Add(("B", "PQRSTUVWXYZ01234567"));
			map.Add(("B", "PQRSTUVWXYZ012345678"));
			map.Add(("B", "PQRSTUVWXYZ0123456789"));

			matcher = new ConfusableMatcher(map, null);

			res = matcher.IndexOf(
				"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
				"BB",
				CMOptions.Default);
			AssertMatch(res, 0, 2);

			var opts = CMOptions.Default;
			opts.MatchRepeating = true;
			opts.TimeoutNs = 5000000;

			res = matcher.IndexOf(
				"PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789PQRSTUVWXYZ0123456789",
				"BBBBBBBBBBBBBBBBBBBBBBBBBBB",
				opts);
			AssertMatch(res, 0, 547);
		}

		[Fact]
		void Test10()
		{
			var opts = CMOptions.Default;
			opts.MatchRepeating = true;
			var map = new List<(string Key, string Value)>();
			var matcher = new ConfusableMatcher(map, null);
			var res = matcher.IndexOf(":)", "", opts);
			AssertMatch(res, 0, 0);

			res = matcher.IndexOf("", ":)", opts);
			AssertNoMatch(res);
		}

		[Fact]
		void Test11()
		{
			var map = new List<(string Key, string Value)>();
			var matcher = new ConfusableMatcher(map, null);

			var res = matcher.IndexOf("A", "A", CMOptions.Default);
			AssertMatch(res, 0, 1);

			var matcher2 = new ConfusableMatcher(map, null, false);
			res = matcher2.IndexOf("A", "A", CMOptions.Default);
			AssertNoMatch(res);
		}

		[Fact]
		void Test12()
		{
			var map = new List<(string Key, string Value)>();
			var matcher = new ConfusableMatcher(map, new[] { "B", " ", "C" });

			var res = matcher.IndexOf("AB CD", "ABCD", CMOptions.Default);
			AssertMatch(res, 0, 5);
		}

		[Fact]
		void Test13()
		{
			var opts = CMOptions.Default;
			opts.MatchRepeating = true;
			var map = new List<(string Key, string Value)>() {
				("N", "/\\/") 
			};
			var ignoreList = new List<string>();
			var matcher = new ConfusableMatcher(map, null);
			bool running = true;
			var @lock = new object();

			var t1 = new Thread(() => {
				while (running) {
					lock (@lock) {
						matcher.IndexOf("/\\/", "N", opts);
					}
				}
			});

			var t2 = new Thread(() => {
				while (running) {
					lock (@lock) {
						if (matcher != null)
							matcher.Dispose();

						matcher = new ConfusableMatcher(map, null, true);
					}

					Thread.Sleep(500);
				}
			});

			t1.Start();
			t2.Start();

			Thread.Sleep(10000);

			running = false;
			t1.Join();
			t2.Join();
		}

		[Fact]
		void Test14()
		{
			var opts = CMOptions.Default;
			opts.StartIndex = 2;
			var map = new List<(string Key, string Value)>() {
				("⿌", "⿌"),
				("⎀", "⎀")
			};

			var matcher = new ConfusableMatcher(map, null);

			var res = matcher.IndexOf("碐랩⿌⎀ꅉᚲ콅讷鷪", "⿌⎀", opts);
			AssertMatch(res, 2, 2);
		}

		[Fact]
		void Test15()
		{
			var map = new List<(string Key, string Value)>() {
				("A", "1"),
				("B", "1"),
				("C", "1")
			};
			var matcher = new ConfusableMatcher(map, null, false);

			Assert.Equal(new[] { "1" }, matcher.GetKeyMappings("A"));
			Assert.Equal(new[] { "1" }, matcher.GetKeyMappings("B"));
			Assert.Equal(new[] { "1" }, matcher.GetKeyMappings("C"));
		}

		[Fact]
		void Test16()
		{
			var map = new List<(string Key, string Value)>() {
				("1", "AB"),
				("1", "CD"),
				("2", "EEE")
			};
			var matcher = new ConfusableMatcher(map, null, false);

			Assert.Equal(new[] { "AB", "CD" }.OrderBy(x => x), matcher.GetKeyMappings("1").OrderBy(x => x));
			Assert.Equal(new[] { "EEE" }, matcher.GetKeyMappings("2"));
		}

		[Fact]
		void Test17()
		{
			var map = new List<(string Key, string Value)>();

			for (var x = 0;x < 500;x++) {
				map.Add(("123", x.ToString()));
			}

			var matcher = new ConfusableMatcher(map, null, false);

			Assert.Equal(map.Select(x => x.Value).OrderBy(x => x), matcher.GetKeyMappings("123").OrderBy(x => x));
		}

		[Fact]
		void Test18()
		{
			var map = GetDefaultMap();

			var opts = CMOptions.Default;
			opts.TimeoutNs = 1;
			opts.MatchRepeating = true;
			var matcher = new ConfusableMatcher(map, null);

			var In = "ASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASASB";

			var res = matcher.IndexOf(In, "ASB", opts);
			Assert.Equal(CM_RETURN_STATUS.TIMEOUT, res.Status);

			opts.StartFromEnd = true;
			opts.StartIndex = (nuint)In.Length - 1;
			res = matcher.IndexOf(In, "ASB", opts);
			AssertMatch(res, 92, 3);
		}

		[Fact]
		void Test19()
		{
			var map = GetDefaultMap();

			var opts = CMOptions.Default;
			opts.MatchRepeating = true;
			opts.TimeoutNs = 5000000;
			var matcher = new ConfusableMatcher(map, new[] { "̇", "̸" });

			var In = "Ṅ̸iggęr";

			var res = matcher.IndexOf(In, "NIGGER", opts);
			AssertMatch(res, 0, 8);
		}

		[Fact]
		void Test31()
		{
			var map = new List<(string Key, string Value)>();

			var opts = CMOptions.Default;
			opts.MatchOnWordBoundary = true;

			var matcher = new ConfusableMatcher(map, null);
			var res = matcher.IndexOf("X", "X", opts);
			AssertMatch(res, 0, 1);

			res = matcher.IndexOf("aX", "X", opts);
			AssertMatch(res, 1, 1, CM_RETURN_STATUS.WORD_BOUNDARY_FAIL_START);

			res = matcher.IndexOf("Xa", "X", opts);
			AssertMatch(res, 0, 1, CM_RETURN_STATUS.WORD_BOUNDARY_FAIL_END);

			res = matcher.IndexOf("a X", "X", opts);
			AssertMatch(res, 2, 1);

			res = matcher.IndexOf("X a", "X", opts);
			AssertMatch(res, 0, 1);

			res = matcher.IndexOf("X;duper", "X", opts);
			AssertMatch(res, 0, 1);

			res = matcher.IndexOf("yes\uFEFFX", "X", opts);
			AssertMatch(res, 4, 1);
		}

		[Fact]
		void Test32()
		{
			var map = new List<(string Key, string Value)>();

			var opts = CMOptions.Default;
			opts.MatchOnWordBoundary = true;
			opts.MatchRepeating = true;

			var matcher = new ConfusableMatcher(map, null);

			var res = matcher.IndexOf("QQQ", "Q", opts);
			AssertMatch(res, 0, 3);

			res = matcher.IndexOf("aQQQ", "Q", opts);
			Assert.True(res.Status == CM_RETURN_STATUS.WORD_BOUNDARY_FAIL_START || res.Status == CM_RETURN_STATUS.WORD_BOUNDARY_FAIL_END);
			AssertMatchMulti(res, new[] { 1, 1, 1, 2, 2, 3 }, new[] { 1, 2, 3, 1, 2, 1 }, res.Status);

			res = matcher.IndexOf("QQQa", "Q", opts);
			Assert.True(res.Status == CM_RETURN_STATUS.WORD_BOUNDARY_FAIL_START || res.Status == CM_RETURN_STATUS.WORD_BOUNDARY_FAIL_END);
			AssertMatchMulti(res, new[] { 0, 0, 0, 1, 1, 2 }, new[] { 1, 2, 3, 1, 2, 1 }, res.Status);

			res = matcher.IndexOf("a QQQ", "Q", opts);
			AssertMatch(res, 2, 3);

			res = matcher.IndexOf("QQQ a", "Q", opts);
			AssertMatch(res, 0, 3);

			res = matcher.IndexOf("QQQ;duper", "Q", opts);
			AssertMatch(res, 0, 3);

			res = matcher.IndexOf("yes\u202FQQQ", "Q", opts);
			AssertMatch(res, 4, 3);
		}

		[Fact]
		void Test33()
		{
			var map = new List<(string Key, string Value)>();
			var @in = "a QBQQ";

			var opts = CMOptions.Default;
			opts.MatchOnWordBoundary = true;
			opts.MatchRepeating = true;
			opts.StartFromEnd = true;
			opts.StartIndex = (nuint)@in.Length - 1;

			var matcher = new ConfusableMatcher(map, new[] { "B" });

			var res = matcher.IndexOf(@in, "Q", opts);
			AssertMatch(res, 2, 4);
		}

		[Fact]
		void Test34()
		{
			var map = new List<(string Key, string Value)>();

			var opts = CMOptions.Default;
			opts.MatchOnWordBoundary = true;
			opts.MatchRepeating = true;

			var matcher = new ConfusableMatcher(map, null);

			var res = matcher.IndexOf("SUPER", "SUPER", opts);
			AssertMatch(res, 0, 5);

			res = matcher.IndexOf("aSUPER", "SUPER", opts);
			AssertMatch(res, 1, 5, CM_RETURN_STATUS.WORD_BOUNDARY_FAIL_START);

			res = matcher.IndexOf("SUPERa", "SUPER", opts);
			AssertMatch(res, 0, 5, CM_RETURN_STATUS.WORD_BOUNDARY_FAIL_END);

			res = matcher.IndexOf("a SUPER", "SUPER", opts);
			AssertMatch(res, 2, 5);

			res = matcher.IndexOf("SUPER a", "SUPER", opts);
			AssertMatch(res, 0, 5);

			res = matcher.IndexOf("SUPER;duper", "SUPER", opts);
			AssertMatch(res, 0, 5);

			res = matcher.IndexOf("yes\u202FSUPER", "SUPER", opts);
			AssertMatch(res, 4, 5);
		}
	}
}
