﻿#r "System.Runtime.InteropServices" 
#I @"C:\Users\cybernetic\source\repos\"
#r @"BlingFire\bin\Release\net5.0\BlingFire.dll"
#r @"Prelude\Prelude\bin\Release\net5\Prelude.dll"
#r @"SentencePieceWrapper\SentencePieceDotNET\bin\x64\Release\net472\SentencePieceDotNET.dll" 
#load "tensor-extensions.fsx"
#load "TestDocSnippets.fsx"
#time "on"

open Newtonsoft.Json
open Microsoft.ML.OnnxRuntime
open System
open Prelude.Common 
open Prelude.Math 
open BlingFire
open MathNet.Numerics
open MathNet.Numerics.LinearAlgebra
open ``Tensor-extensions`` 

let vocab =
    let vocabDict =
        JsonConvert.DeserializeObject<Collections.Generic.Dictionary<string, int>>
            (IO.File.ReadAllText
                 @"D:\Downloads\NeuralNets\roberta-base-squad2\vocab.json")
    [| for KeyValue(w, _) in vocabDict -> w |]

//Test that the library is working at all.
Runtime.InteropServices.NativeLibrary.Load(@"D:Downloads\blingfiretokdll.dll")

let robertaTokHandle = BlingFireUtils.LoadModel  @"C:/Users/cybernetic/source/repos/BlingFire/bin/Release/net5.0/Files/roberta.bin"
let bertTokHandle = BlingFireUtils.LoadModel  @"C:/Users/cybernetic/source/repos/BlingFire/bin/Release/net5.0/Files/bert_base_tok.bin"

///////////
// allocate space for ids 
let ids = Array.create 1024 0

let inBytes = Text.Encoding.UTF8.GetBytes("hello this is a test</s>");

let _ = BlingFireUtils.TextToIds (robertaTokHandle, inBytes.AsSpan(), inBytes.Length, ids.AsSpan(),  ids.Length, 0)
let _ = BlingFireUtils.TextToIds (bertTokHandle, inBytes.AsSpan(), inBytes.Length, ids.AsSpan(),  ids.Length, 0)

ids

//Test sentence splitting
BlingFireUtils.GetSentences "This is a test of 3.45 people with Ph.D's said\n Mr. Robert. Who will take this."
|> Seq.toArray
|> printfn "%A"

/////////////////////////////

let startToken, stopToken = 0, 2

let appendStartEndTokens s = [|startToken; yield! s; stopToken|]

///The current blingfire tokenizer behaves as hugging-face's with ``add_prefix_space=True``
let generalTokenizer (tokenizerHandle: uint64) (s: string) =
    let inBytes = Text.Encoding.UTF8.GetBytes(s)

    let ids = Array.create inBytes.Length 0

    let outlen = BlingFireUtils.TextToIds(tokenizerHandle, inBytes.AsSpan(), inBytes.Length, ids.AsSpan(), ids.Length, 3)
     
    ids.[..outlen - 1] 
    
//Renaming for semantic purposes
let robertaTokenizer (s: string) =
    generalTokenizer robertaTokHandle s 
      
let bartTokenizer (s: string) =
    generalTokenizer robertaTokHandle s  

let bertTokenizer (s: string) =
    generalTokenizer bertTokHandle s  

let robertaDetokenizer (vocab:string []) tokens = 
    let inline lookUpFixHack (s:string) = s.Replace("âĢĻ", "’").Trim(' ')
    tokens 
    |> Seq.fold (fun str t -> 
        let w = vocab.[t] 
        if w.[0] = 'Ġ' then str + " " + w.[1..] 
        else str + w) ""
    |> lookUpFixHack 
     
//////////////////

//The blingfire encoder removes newlines. Transformer LMs are quite sensitive to whitespace
bartTokenizer ("A\nB")

let toks = robertaTokenizer "I don't think that's ribonucleic"
        
robertaDetokenizer vocab toks

///Appending tokens test
let w1 = robertaTokenizer "Hello there"

appendStartEndTokens w1 |> robertaDetokenizer vocab

////////////////////

//The problem of decoding unicode characters. Using vocab.json seems to result in mangling and using a lookup table will not scale.
//How to fix? State: Don't know.
//

[|for t in robertaTokenizer "漢" do 
    yield! vocab.[t].Replace("Ġ", "").ToCharArray() |]
|> Array.map byte 
|> Strings.DecodeFromUtf8Bytes

Strings.toUTF8Bytes "漢"

[|for t in robertaTokenizer "’" do 
    yield! vocab.[t].Replace("Ġ", "").ToCharArray() |]
|> Array.map byte 
|> Strings.DecodeFromUtf8Bytes

[|for t in robertaTokenizer "Inside the apple’s" do 
    yield vocab.[t].Replace("Ġ", "")|]

Strings.toUTF8Bytes "’"

Strings.toUTF8Bytes "ë"

Array.map2 (-) [|229uy; 173uy; 151uy|] [|229uy; 67uy; 57uy|]

Array.map2 (-) [|226uy; 128uy; 153uy|] [|226uy; 34uy; 59uy|]

Array.map2 (-) [|226uy; 134uy; 144uy|] [|226uy; 40uy; 50uy|]

[|for t in robertaTokenizer "字" -> vocab.[t]|]

[|for t in robertaTokenizer "漢" -> vocab.[t]|]

"æ¼¢åŃĹ".ToCharArray() |> Array.map byte |> Strings.DecodeFromUtf8Bytes

"âĸ²".ToCharArray() |> Array.map byte //|> Strings.DecodeFromUtf8Bytes

let deMangleUnicode (str:string) =
    str.ToCharArray()
    |> Array.mapi (fun i c ->
        let b = byte c
        if i = 0 then b
        else 
            if (int b) + 94 > 255 then b
            else b + 94uy)
    |> Strings.DecodeFromUtf8Bytes

["âĢĿ"; "âĸ²"; "åŃĹ"; "âĢľ";"æ¼¢"]
|> List.map deMangleUnicode

Strings.toUTF8Bytes "▲"

Seq.map Char.IsLetterOrDigit "æ¼¢"

["âĢĿ"; "âĸ²"; "åŃĹ"; "âĢľ"] |> List.map (Seq.map Char.IsLetterOrDigit)

///////////////////////// 
 
let robertaQA = new InferenceSession(@"D:\Downloads\NeuralNets\roberta-base-squad2\roberta-base-squad2.onnx")
 
robertaQA.OutputMetadata

//////////////////////////////////////
//Stats on token size vs words len

let testWordsSplit (s:string) =
    robertaTokenizer s |> Array.length,  
    s.Split(' ') |> Seq.length

[581./727.; 788./1032.;244./322.; 530./741.; 565./771.; 2073./ 3270. ]

//////////////////
"I am going to the store. I bought a thing." |> robertaTokenizer 
    = ([|"I am going to the store."; "I bought a thing."|] 
         |> Array.collect robertaTokenizer)

module BlingFireUtils = 
    let splitSentence (str:string) =  
        let sents = BlingFireUtils.GetSentences str |> Seq.toArray 
        //I'm not sure why blingfire adds a weird token to the last sentence
        sents.[^0] <- sents.[^0].[..^1]
        sents 

(*
All right so the basic idea here is that we split the document up into sentences and then tokenize each sentence. 
There are several ways to go about this. One way is to recurse over the sentence and have a temporary stack of the current set of tokens and then a more permanent stack of all the full token windows. 

The problem is that each of these arrays of tokens need to be concatenated but if we're doing that we might as well do it more clearly. One way to do this is to have an inner function build a current sentence so far and then returns the sentences that no longer fit and yields that array. Then recurse again on the rest of the sentences, repeat the above and so on and so forth recursively.
*) 

let tokenize maxlen i0 (sents: _ []) =
    //Actually, I will use a buffer approach.
    //Recursive for early exit.
    let buffer = Array.create maxlen stopToken
    buffer.[0] <- startToken

    let rec loop i tokenlen =
        let sent = sents.[i]
        let tokenized = robertaTokenizer sent
        let tokenlen' = tokenlen + tokenized.Length

        if tokenlen' + 1 < maxlen then // account for stop tag
            Array.Copy(tokenized, 0, buffer, tokenlen, tokenized.Length)
            if i + 1 < sents.Length then loop (i + 1) tokenlen'
            else i + 1, tokenlen' //done
        else i, tokenlen

    let i, toklen = loop i0 1
    buffer.[..toklen], i
     
///Transformers have windows of size = N tokens max. To get around this, rather 
///than just truncate we split by sentence and join sentences until they exhaust the window
///and then start a new window until all windows have been complete. Think of trying to 
///write a tweet thread were all tweets must contain only complete sentences.
let tokenizeSplit maxlen (sents : _ []) =
    let rec loop i =
        [| if i < sents.Length then 
            let s, j = tokenize maxlen i sents  
            if s.Length > 2 then yield s
            // if i = j and there're more sents left then a sentence was probably too long. We can either fail or skip. Given token window lens are > 300 to 600 words, I will choose to skip.
            let j' =
                if i = j then j + 1
                else j
            yield! loop j' |]
    loop 0

let sents = BlingFireUtils.splitSentence TestDocSnippets.reals  

let maxlen = 512// = 0

tokenizeSplit 25 [|""|]
tokenizeSplit 25 [|"A"|]
tokenizeSplit 12 sents
tokenizeSplit 15 sents 
tokenizeSplit 20 sents 
tokenizeSplit 32 sents 
tokenizeSplit 50 sents 
tokenizeSplit 512 sents 

sents.Length

let i = 0
let tokenlen= 1

let stripTags (a:_[]) = a.[1..^1]

tokenizeSplit 50 sents
|> Array.collect stripTags = robertaTokenizer TestDocSnippets.reals

tokenizeSplit 512 sents 
|> Array.concat
|> stripTags = robertaTokenizer TestDocSnippets.reals 

Array.map robertaTokenizer sents

Array.map (robertaTokenizer >> Array.length) sents //'|> Array.sum
  
let formatforSquadQATruncate (context:string, question:string) = 
    let truncate (s:string) =   
        let split = s.Split(' ')
        if split.Length > 360 then split.[..359] |> String.concat " " else s

    $"<s> {question} </s> <s> {truncate context} </s>"

let formatforSquadQA (context:string, question:string) = 
    let splitContext = context.Split(' ') |> Array.split_TakeN_atATime 360  
    [|for context in splitContext -> 
        let bcontext = String.concat " " context
        $"<s> {question} </s> <s> {bcontext} </s>"|] 

formatforSquadQA ("ABC", "EFD") |> Seq.head |> robertaTokenizer 

let runrobertaQA (robertaQA:InferenceSession) (tokens:int []) =
    let t = Tensors.ArrayTensorExtensions.ToTensor(array2D [|tokens|])

    let outputs = robertaQA.Run [|NamedOnnxValue.CreateFromTensor("input_ids", t)|]
    let res =
        [| for output in outputs -> 
            let result = output.AsTensor<float32>()

            let dims = result.Dimensions.ToArray()

            let vec = [| for i in 0..dims.[1] - 1 -> result.[0, i] |]
            
            output.Dispose()
            vec |]
    
    outputs.Dispose()

    {|Start = res.[0]; End = res.[1]|}

////////////////////

let qas =  
    formatforSquadQA ("I picked an apple, ate it then went next door to pick a banana.", 
        "What door did I go through ?")
    |> Array.map robertaTokenizer 
let answers = Array.map (runrobertaQA robertaQA) qas

answers.[0].Start 
|> Array.map exp 
|> Array.normalize
|> Array.mapi (fun i p -> robertaDetokenizer vocab [qas.[0].[i]], p)
|> Prelude.TextHistogram.genericHistogram float 20 

//////////////////////////////

let str = IO.File.ReadAllText (pathCombine DocumentsFolder "doc.txt")

let qas = 
    formatforSquadQA (str, 
        "What is the main idea of this research ?")
    |> Array.map (robertaTokenizer >> Array.head)

let answers = Array.map (runrobertaQA robertaQA) qas

[| for n in 0 .. 2 do
    [| for (i, a) in Array.zip [| 0 .. answers.Length - 1 |] answers do
        let start, sp = Array.nthTop n a.Start
        let endIndex, ep = Array.nthTop n a.End

        if exp sp > 1.f then
            let str =
                qas.[i].[start..endIndex] |> robertaDetokenizer vocab

            if str <> "" && not (str.Contains "<s>") then
                yield str, exp sp, exp ep |] |]

BlingFireUtils.GetWords str 
|> Seq.toArray 
|> Array.countBy id 
|> Array.sortByDescending snd 

str |> BlingFireUtils.GetSentences |> Seq.toArray

/////////////////////////

/////////////////////// 

let bartModelEncoder = new InferenceSession(@"D:\Downloads\NeuralNets\bart-large-cnn\bart-large-cnn-encoder.onnx")

let bartModelDecoder = new InferenceSession(@"D:\Downloads\NeuralNets\bart-large-cnn\bart-large-cnn-decoder.onnx")

let tok0 = bartTokenizer "This is a test." 

let tok0 = [|[|152; 16; 10; 1296; 1437|]|]
 
let toks = bartTokenizer str |> Array.map appendStartEndTokens

let t = Tensors.ArrayTensorExtensions.ToTensor(array2D tok0)

t

robertaDetokenizer vocab tok0.[0]

let outputs = bartModelEncoder.Run [|NamedOnnxValue.CreateFromTensor("input_ids", t)|]

let encoderStates = Seq.head outputs

encoderStates.Name <- "encoder_hidden_states"
 
robertaDetokenizer vocab [50118] 

outputs.Dispose()

encoderStates.Dispose()  


let greedySample (decoder:InferenceSession) maxlen encoderStates =
    
    let generatedTokens = ResizeArray startToken
    let mutable generated = Tensors.ArrayTensorExtensions.ToTensor(array2D [ [ startToken ] ])

    let mutable c = 0
    let mutable stop = false
    let sw = Diagnostics.Stopwatch()

    while c < maxlen && not stop do 
        let logits =
            decoder.Run 
                [| NamedOnnxValue.CreateFromTensor("input_ids", generated)
                   encoderStates |] 

        let decoded = Seq.head logits

        let result = decoded.AsTensor<float32>()   
         
        let index = Array.argmaxf32 result.[0, ^0, *] 

        if index <> stopToken then generatedTokens.Add index  
        
        generated <- 
            Tensors.ArrayTensorExtensions.ToTensor(array2D [| generatedTokens.ToArray() |])
             
        decoded.Dispose()

        logits.Dispose()

        sw.Stop() 
        c <- c + 1
        stop <- index = stopToken

    robertaDetokenizer vocab (generatedTokens.ToArray())

greedySample bartModelDecoder 50 encoderStates

//TODO improve splitting

let runSummary (encoder: InferenceSession) (decoder: InferenceSession) seqlen toks =
    [| for tok in toks do
        let t =
            Tensors.ArrayTensorExtensions.ToTensor(array2D [| tok |])

        let outputs =
            encoder.Run [| NamedOnnxValue.CreateFromTensor("input_ids", t) |]

        let encoderStates = Seq.head outputs

        encoderStates.Name <- "encoder_hidden_states"

        let str =
            greedySample decoder seqlen encoderStates

        encoderStates.Dispose()
        outputs.Dispose()
        yield str |] 

let distilbartModelEncoder = new InferenceSession(@"D:\Downloads\NeuralNets\distilbart-cnn-6-6\distilbart-cnn-6-6-encoder.onnx")

let distilbartModelDecoder = new InferenceSession(@"D:\Downloads\NeuralNets\distilbart-cnn-6-6\distilbart-cnn-6-6-decoder.onnx")

let tokens = bartTokenizer str |> Array.map appendStartEndTokens

let ss1 = runSummary bartModelEncoder bartModelDecoder 200 tokens

let ss2 = runSummary distilbartModelEncoder distilbartModelDecoder 200 tokens

ss1

ss2