// swift-tools-version: 6.3

// depot-render draws a UIScript document with Hudini, for Depot's canvas. It uses the Hudini checkout beside
// VentureOS while Depot lives in VentureOS's depot.local/, and moves to Hudini's main branch before Depot leaves.

import PackageDescription

let package = Package(
    name: "depot-render",
    dependencies: [
        // The name keeps the product references below working whatever the checkout's folder is called.
        .package(name: "Hudini", path: "../../../Hudini"),
        .package(url: "https://github.com/tonytins/raylib-swift", branch: "main"),
        .package(url: "https://github.com/apple/swift-argument-parser", from: "1.5.0"),
    ],
    targets: [
        // What can be tested without a window (requests, themes, and frame maps), and the renderer, which needs one.
        .target(
            name: "DepotRender",
            dependencies: [
                .product(name: "Hudini", package: "Hudini"),
                .product(name: "Raylib", package: "raylib-swift"),
            ]
        ),
        .executableTarget(
            name: "depot-render",
            dependencies: [
                "DepotRender",
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
            ]
        ),
        .testTarget(
            name: "DepotRenderTests",
            dependencies: [
                "DepotRender",
                .product(name: "Hudini", package: "Hudini"),
            ]
        ),
    ],
    swiftLanguageModes: [.v6]
)
