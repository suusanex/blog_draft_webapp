# 実生成本文

経路: workflow（アウトライン確定後の本文生成）
モデル: `gpt-5.6-sol`
ステータス: 成功

```markdown
# WPF の Slider

WPF には、数値を指定した範囲内で変更できる `Slider` があります。網羅的な解説は公式資料に委ね、ここでは直感的に設定して躓いた点を共有します。

https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.controls.slider?view=windowsdesktop-10.0

## 直感的な設定で遭遇した挙動

当初は `Minimum`、`Maximum`、`TickFrequency` を設定すればよいと考えました。例えば範囲を 0〜1、目盛りを 0.1 に設定して試しました。

レーンをクリックすれば 1 目盛りずつ動くと予想していましたが、実際には最小値と最大値を往復しました。一方、キーボードでは 1 目盛りずつ動いているように見えました。

## TickFrequency と移動幅は別

`TickFrequency` が表すのは目盛りであり、操作による移動幅とは別です。目盛りの間隔を設定しただけでは、レーンのクリックやキーボード操作による移動幅は決まりません。

### LargeChange と SmallChange

レーンをクリックしたときの移動幅は `LargeChange` で、既定値は 1 です。0〜1 の範囲で最小値と最大値を往復したのは、この値が範囲全体と同じ 1 だったためです。

キーボード操作の移動幅は `SmallChange` で、既定値は 0.1 です。今回は `TickFrequency` も 0.1 だったため、キーボードでは目盛りに従って動いているように見えましたが、これは値が偶然一致していただけでした。

意図した挙動にするには、`TickFrequency` だけでなく `LargeChange` と `SmallChange` も設定します。
```
