using System.Runtime.CompilerServices;

// Game.Tests.MVC / Game.Tests.PlayMode から Game.MVC.Core アセンブリの internal 型を直接利用できるようにする。
// テスト専用の可視性拡張であり、上記以外の外部アセンブリからは引き続き internal として不可視
[assembly: InternalsVisibleTo("Game.Tests.MVC")]
[assembly: InternalsVisibleTo("Game.Tests.PlayMode")]
