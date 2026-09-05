数値を範囲内で変える Slider を紹介する。WPF に存在すること自体を知らない読者にも触れる。網羅的な解説は次の資料へ委ね、直感的に使って躓いた点を共有する。

https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.controls.slider?view=windowsdesktop-10.0

Minimum、Maximum、TickFrequency を設定すればよいと考え、例えば 0〜1、目盛り 0.1 で試した。レーンをクリックすると 1 目盛りずつ動く予想に反して最小値と最大値を往復する一方、キーボードでは 1 目盛りずつ動いているように見えた。

TickFrequency は目盛りであり操作による移動幅とは別。クリックの LargeChange の既定値 1 とキーボードの SmallChange の既定値 0.1 が挙動の理由。キーボードで動いたのは偶然の一致だった。TickFrequency だけでなく LargeChange と SmallChange も設定する。
