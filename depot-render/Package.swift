// swift-tools-version: 6.3

// depot-render draws a UIScript document with Hudini, for Depot's canvas. It follows Hudini's main branch, which has
// the depot and depotLight themes it draws in.

import PackageDescription

let package = Package(
    name: "depot-render",
    dependencies: [
        .package(url: "https://github.com/tonytins/Hudini", branch: "main"),
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
