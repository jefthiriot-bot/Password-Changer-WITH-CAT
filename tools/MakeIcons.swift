// Resolution-independent native icon drawing; no external packages required.
import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

let destination = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
try FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true)
func color(_ hex: UInt32) -> CGColor {
    return CGColor(red: CGFloat((hex >> 16) & 255) / 255, green: CGFloat((hex >> 8) & 255) / 255, blue: CGFloat(hex & 255) / 255, alpha: 1)
}
for reset in [false, true] {
    let name = reset ? "PasswordPuzzle-Reset" : "PasswordPuzzle"
    for size in [16, 24, 32, 48, 64, 128, 256, 512] {
        let context = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: size * 4,
            space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        context.scaleBy(x: CGFloat(size) / 256, y: CGFloat(size) / 256)
        context.translateBy(x: 0, y: 256); context.scaleBy(x: 1, y: -1)
        func rounded(_ rect: CGRect, _ radius: CGFloat, _ fill: UInt32) {
            context.setFillColor(color(fill)); context.addPath(CGPath(roundedRect: rect, cornerWidth: radius, cornerHeight: radius, transform: nil)); context.fillPath()
        }
        rounded(CGRect(x: 8, y: 8, width: 240, height: 240), 52, reset ? 0xA93620 : 0x173B78)
        rounded(CGRect(x: 16, y: 16, width: 224, height: 108), 44, reset ? 0xC94B2C : 0x2458A4)
        // White lock with a clear shackle and four password dots.
        context.setStrokeColor(color(0xFFFFFF)); context.setLineWidth(17); context.setLineCap(.round)
        context.move(to: CGPoint(x: 77, y: 108)); context.addLine(to: CGPoint(x: 77, y: 80))
        context.addCurve(to: CGPoint(x: 159, y: 80), control1: CGPoint(x: 77, y: 26), control2: CGPoint(x: 159, y: 26))
        context.addLine(to: CGPoint(x: 159, y: 108)); context.strokePath()
        rounded(CGRect(x: 52, y: 104, width: 132, height: 102), 22, 0xFFFFFF)
        context.setFillColor(color(reset ? 0xA93620 : 0x173B78))
        for x in [72, 96, 120, 144] { context.fillEllipse(in: CGRect(x: x, y: 139, width: 12, height: 12)) }
        // Rotation arrow: distinguishes this from a generic password manager.
        context.setFillColor(color(reset ? 0xFFCF70 : 0x35D8BB))
        context.fillEllipse(in: CGRect(x: 140, y: 142, width: 100, height: 100))
        context.setStrokeColor(color(reset ? 0x762A18 : 0x12394A)); context.setLineWidth(10)
        context.addArc(center: CGPoint(x: 190, y: 192), radius: 27, startAngle: -.pi / 3, endAngle: .pi * 1.32, clockwise: false); context.strokePath()
        context.setFillColor(color(reset ? 0x762A18 : 0x12394A))
        context.move(to: CGPoint(x: 209, y: 160)); context.addLine(to: CGPoint(x: 211, y: 185)); context.addLine(to: CGPoint(x: 189, y: 173)); context.closePath(); context.fillPath()
        if !reset && size >= 32 {
            // Small math mark beside the lock, retained at useful desktop sizes.
            context.setStrokeColor(color(0xA5E9FF)); context.setLineWidth(5)
            context.move(to: CGPoint(x: 198, y: 66)); context.addLine(to: CGPoint(x: 218, y: 66))
            context.move(to: CGPoint(x: 208, y: 56)); context.addLine(to: CGPoint(x: 208, y: 76)); context.strokePath()
        }
        let path = destination.appendingPathComponent("\(name)-\(size).png")
        let output = CGImageDestinationCreateWithURL(path as CFURL, UTType.png.identifier as CFString, 1, nil)!
        CGImageDestinationAddImage(output, context.makeImage()!, nil)
        if !CGImageDestinationFinalize(output) { fatalError("Could not write icon") }
    }
}
