# 実生成本文

経路: one-shot `/draft`
モデル: `gpt-5.6-sol`
ステータス: 成功

```markdown
# WPF Slider の目盛りと移動幅は別に設定する

WPF には、数値を指定した範囲内で変更できる `Slider` がある。網羅的な使い方は公式資料に委ね、今回は直感的に設定して躓いた点を共有する。

https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.controls.slider?view=windowsdesktop-10.0

## TickFrequency は操作時の移動幅ではない

`Minimum`、`Maximum`、`TickFrequency` を設定すればよいと考え、範囲を 0〜1、目盛りを 0.1 にして試した。

レーンをクリックすると 1 目盛りずつ動くと予想していたが、実際には最小値と最大値を往復した。一方、キーボードでは 1 目盛りずつ動いているように見えた。

原因は、`TickFrequency` が目盛りの間隔であり、操作による移動幅とは別の設定だからだった。クリック時に使われる `LargeChange` の既定値は 1、キーボード操作で使われる `SmallChange` の既定値は 0.1 なので、この設定では次の挙動になる。

- レーンのクリックでは 1 ずつ移動し、0〜1 の範囲では最小値と最大値を往復する
- キーボードでは 0.1 ずつ移動し、目盛りと一致して見える

キーボードで意図どおりに動いたのは、`TickFrequency` と `SmallChange` が偶然一致していただけだった。目盛りと操作時の移動幅をそろえたい場合は、`TickFrequency` だけでなく `LargeChange` と `SmallChange` も設定する。
```
