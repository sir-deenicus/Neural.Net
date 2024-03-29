﻿//#r "System.Memory"
#I @"C:\Users\cybernetic\source\repos\"
#r @"D:\Downloads\NeuralNets\onnx1.8\Microsoft.ML.OnnxRuntime.dll"
#I @"C:\Users\cybernetic\.nuget\packages\"
#r @"mathnet.numerics\4.15.0\lib\netstandard2.0\MathNet.Numerics.dll"
#r @"mathnet.numerics.fsharp\4.15.0\lib\netstandard2.0\MathNet.Numerics.FSharp.dll"
#r @"Prelude\Prelude\bin\Release\net5\Prelude.dll"

open MathNet.Numerics.LinearAlgebra
open Microsoft.ML.OnnxRuntime
open MathNet.Numerics
open System
open Prelude.Math 


type Tensors.Tensor<'a> with  
    member t.GetSlice(r:int,startIdx2:int option, endIdx2 : int option) =
        let dim = t.Dimensions.ToArray()
        if dim.Length <> 2 then failwith "Dimensions must be = 2"

        let sidx2 = defaultArg startIdx2 0
        let endidx2 = defaultArg endIdx2 (dim.[1] - 1) 
        [|for c in sidx2..endidx2  -> t.[r, c]|]

    member t.GetSlice(startIdx1:int option, endIdx1 : int option, c:int) =
        let dim = t.Dimensions.ToArray()
        if dim.Length <> 2 then 
            failwith "Dimensions must be = 2"

        let sidx1 = defaultArg startIdx1 0
        let endidx1 = defaultArg endIdx1 (dim.[0] - 1) 
        [|for r in sidx1..endidx1 -> t.[r,c]|]  
    
    member t.GetSlice(startIdx1:int option, endIdx1 : int option,startIdx2:int option, endIdx2 : int option) =
        let dim = t.Dimensions.ToArray()
        if dim.Length <> 2 then failwith "Dimensions must be = 2"

        let sidx1 = defaultArg startIdx1 0
        let sidx2 = defaultArg startIdx2 0
        let endidx1 = defaultArg endIdx1 (dim.[0] - 1)
        let endidx2 = defaultArg endIdx2 (dim.[1] - 1)
        [| for r in sidx1..endidx1 -> [|for c in sidx2..endidx2 -> t.[r,c]|]|]
 
    member t.GetSlice(m:int,r:int, startIdx3:int option, endIdx3 : int option) =
        let dim = t.Dimensions.ToArray()
        if dim.Length <> 3 then failwith "Dimensions must be = 3"

        let sidx3 = defaultArg startIdx3 0
        let endidx3 = defaultArg endIdx3 (dim.[2] - 1) 
        [|for c in sidx3..endidx3 -> t.[m,r, c]|] 

    member t.GetSlice(m:int,startIdx2:int option, endIdx2 : int option,startIdx3:int option, endIdx3 : int option) =
        let dim = t.Dimensions.ToArray()
        if dim.Length <> 3 then failwith "Dimensions must be = 3"

        let sidx2 = defaultArg startIdx2 0
        let endidx2 = defaultArg endIdx2 (dim.[1] - 1)
        let sidx3 = defaultArg startIdx3 0
        let endidx3 = defaultArg endIdx3 (dim.[2] - 1)
        [|for r in sidx2..endidx2 -> [|for c in sidx3..endidx3  -> t.[m,r, c]|]|]

    member t.GetReverseIndex(i, offset) = 
        let d = t.Dimensions.ToArray()
        int d.[i] - 1 - offset


module Tensor =
    let toJaggedArray2D (t : Tensors.Tensor<_>) =
        let dims = t.Dimensions.ToArray()
        match dims with
        | [| w; h|] ->
            [| for i in 0..w - 1 ->
                [| for j in 0..h - 1 -> t.[i, j] |] |]
        
        | [| 1; w; h |] ->
            [| for i in 0..w - 1 ->
                [| for j in 0..h - 1 -> t.[0, i, j] |] |]
        
        | [| 1; 1; w; h |] ->
            [| for i in 0..w - 1 ->
                [| for j in 0..h - 1 -> t.[0, 0, i, j] |] |]
        | _ -> failwith "Incompatible dimensions"
        
    let toJaggedArray3D (t : Tensors.Tensor<_>) =
        let dims = t.Dimensions.ToArray()
        match dims with
        | [| d; w; h |] ->
            [| for i in 0..d - 1 ->
                [| for j in 0..w - 1 ->
                    [| for k in 0..h - 1 -> t.[i, j, k] |] |] |]
                    
        | [| 1; d; w; h |] ->
            [| for i in 0..d - 1 ->
                [| for j in 0..w - 1 ->
                    [| for k in 0..h - 1 -> t.[0, i, j, k] |] |] |] 
                    
        | _ -> failwith "Incompatible dimensions"
        
    let toArray3D (t : Tensors.Tensor<_>) =
        let dims = t.Dimensions.ToArray()
        match dims with
        | [| d; w; h |] ->
            Array3D.init d w h (fun i j k -> t[i,j,k])
        | [|1 ; d; w; h|] -> Array3D.init d w h (fun i j k -> t[0, i,j,k])
            
        | _ -> failwith "Incompatible dimensions"

    let toArray2D t = array2D (toJaggedArray2D t)

    let toMatrix t = matrix (toJaggedArray2D t)


module Array =
    let argmax minvalue (d:_[]) =
        let mutable topIndex = 0
        let mutable topScore = minvalue

        for i in 0..d.Length - 1 do
            if d.[i] > topScore then 
                topScore <- d.[i]
                topIndex <- i    
        topIndex, topScore

    let argmaxf32 d = argmax Single.MinValue d

    let inline nthTop n d =
        Array.indexed d 
        |> Array.sortByDescending snd 
        |> Array.skip n
        |> Array.head 


let m1 = DenseMatrix.randomStandard 4 3 : Matrix<float32>

m1

let tm1 =
    m1.ToArray()
    |> Tensors.ArrayTensorExtensions.ToTensor

let m3 =
    Array3D.init 1 3 4 (fun _ _ _ -> random.NextDouble() |> float32)

let tm3 =
    m3 |> Tensors.ArrayTensorExtensions.ToTensor
 
m3.[0, *, *]

tm3.Dimensions.ToArray()

tm3.[0, ^0, *]
m3.[0,*,*]

tm3.Dimensions.ToArray()

tm3.[0, ^0, *] |> Array.argmaxf32

tm1.[^1, ^0]

tm1.Dimensions.ToArray()

tm1.[1..,*]

tm1.[*, 1]

tm1.[1, *]

tm3.[0,1]

tm1.[*, ^1..] |> DenseMatrix.ofRowArrays

tm1.[3, 1]

let aa = Array4D.create 3 3 3 3 3
aa |> sprintf "%A"
 