// QR コード画像を作り、読み取れることを確かめる（macOS 標準の CoreImage だけを使う）。
// 使い方: swift Tools/make-qr.swift <URL> <出力.png>
// 例: swift Tools/make-qr.swift https://example.com Assets/StreamingAssets/Presentations/〈プレゼン名〉/qr.png
import AppKit
import CoreImage

let args = CommandLine.arguments
guard args.count == 3 else { fatalError("usage: make-qr.swift <URL> <out.png>") }
let url = args[1]

let filter = CIFilter(name: "CIQRCodeGenerator")!
filter.setValue(url.data(using: .utf8)!, forKey: "inputMessage")
filter.setValue("M", forKey: "inputCorrectionLevel")
let qr = filter.outputImage!

// 1マスを整数倍に拡大し、周囲に4マス分の白い余白を付ける
let modules = Int(qr.extent.width)
let scale = max(1, 1024 / (modules + 8))
let size = (modules + 8) * scale
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size, bitsPerSample: 8,
                           samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                           colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
let context = NSGraphicsContext(bitmapImageRep: rep)!
NSGraphicsContext.current = context
context.cgContext.interpolationQuality = .none
NSColor.white.setFill()
NSRect(x: 0, y: 0, width: size, height: size).fill()
let cg = CIContext().createCGImage(qr, from: qr.extent)!
context.cgContext.draw(cg, in: CGRect(x: 4 * scale, y: 4 * scale, width: modules * scale, height: modules * scale))
context.flushGraphics()
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: args[2]))

let detector = CIDetector(ofType: CIDetectorTypeQRCode, context: nil, options: [CIDetectorAccuracy: CIDetectorAccuracyHigh])!
let decoded = detector.features(in: CIImage(contentsOf: URL(fileURLWithPath: args[2]))!)
    .compactMap { ($0 as? CIQRCodeFeature)?.messageString }
guard decoded == [url] else { fatalError("読み取り結果が一致しません: \(decoded)") }
print("OK \(modules)x\(modules) マス, \(size)px: \(decoded[0])")
