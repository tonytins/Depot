// swift-tools-version: 6.3

// depot-render draws a UIScript document with Hudini, for Depot's canvas. It uses the Hudini checkout beside
// VentureOS while Depot lives in VentureOS's depot.local/, and moves to Hudini's main branch before Depot leaves.

import PackageDescription

let package = Package(
    name: "depot-render",
    dependencies: [
        // The name keeps the product reference below working whatever the checkout's folder is called.
        .package(name: "Hudini", path: "../../../Hudini"),
    ],
    targets: [
        .executableTarget(
            name: "depot-render",
            dependencies: [
                .product(name: "Hudini", package: "Hudini"),
            ]
        ),
    ],
    swiftLanguageModes: [.v6]
)
