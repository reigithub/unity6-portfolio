using System.Runtime.CompilerServices;

// Game.Tests.MVC.Horror から Game.MVC.Horror アセンブリの internal 型を直接利用できるようにする。
// テスト専用の可視性拡張であり、Game.Tests.MVC.Horror 以外の外部アセンブリからは引き続き internal として不可視
[assembly: InternalsVisibleTo("Game.Tests.MVC.Horror")]
