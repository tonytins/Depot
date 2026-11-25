import Foundation
import Hudini

// Nothing to draw yet: drawing a document comes next. Until then it says so on standard error, which stays apart
// from the picture and frame map it'll write later, and fails, so nothing mistakes it for a render that worked.
FileHandle.standardError.write(Data("depot-render can't draw documents yet.\n".utf8))
exit(1)
