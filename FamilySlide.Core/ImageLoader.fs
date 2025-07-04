namespace FamilySlide.Core

open System.IO
open SixLabors.ImageSharp

module ImageLoader =

    let supportedExtensions = [ ".jpg"; ".jpeg"; ".png"; ".webp"; ".bmp"; ".gif" ]

    let getFirstImagePath (folderPath: string) =
        Directory
            .EnumerateFiles(folderPath)
            |> Seq.filter (fun f -> supportedExtensions |> List.exists (fun ext -> f.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase)))
            |> Seq.tryHead
