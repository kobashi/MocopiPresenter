// 画像に写っている QR コードを読み取って表示する（表示確認用）。使い方: swift Tools/read-qr.swift <画像>
import CoreImage
let image = CIImage(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1]))!
let detector = CIDetector(ofType: CIDetectorTypeQRCode, context: nil, options: [CIDetectorAccuracy: CIDetectorAccuracyHigh])!
for case let f as CIQRCodeFeature in detector.features(in: image) { print(f.messageString ?? "") }
