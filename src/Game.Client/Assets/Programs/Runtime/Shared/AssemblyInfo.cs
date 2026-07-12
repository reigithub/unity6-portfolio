using System.Runtime.CompilerServices;

// Game.Tests.Shared から Game.Shared アセンブリの internal 型を直接利用できるようにする。
// テスト専用の可視性拡張であり、Game.Tests.Shared以外の外部アセンブリからは引き続きinternalとして不可視
[assembly: InternalsVisibleTo("Game.Tests.Shared")]
