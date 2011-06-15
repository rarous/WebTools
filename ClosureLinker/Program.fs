open System
open System.Collections
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq
open Microsoft.FSharp.Collections

type Source =
  { Path: string; Provides: list<string>; Requires: list<string>; Text: string }
  override x.ToString() = x.Path

type Output = { Namespace: string; Flags: string list; File: string }
type Flags = { Namespaces: string list; Flags: string list }
type Options = { Root: string; Flags: Flags list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Options =
  let private getDescendants element (xml: XDocument) = xml.Descendants(XName.Get element)
  let private getAttributeValue attr (el: XElement) =
    match el.Attribute(XName.Get attr) with
      | null -> ""
      | a -> a.Value
  let private parseRoot (xml: XDocument) = (xml |> getDescendants "root" |> Seq.head).Value

  let private parseNamespaces (el: XElement) =
    (el |> getAttributeValue "namespaces").Split ';'
      |> Seq.map (fun ns -> ns.Trim())
      |> Seq.filter (fun ns -> not (String.IsNullOrEmpty ns))
      |> Seq.toList

  let private parseFlags (el: XElement) =
    el.Value.Split '\n'
      |> Seq.map (fun f -> f.Trim())
      |> Seq.filter (fun f -> not (String.IsNullOrEmpty f))
      |> Seq.toList

  let private parseFlagsList (xml: XDocument) =
    xml |> getDescendants "flags" 
      |> Seq.map (fun f -> { Namespaces = parseNamespaces f; Flags = parseFlags f })
      |> Seq.toList

  let parse inputFile =
    let xml = File.ReadAllText inputFile |> XDocument.Parse
    { Root = parseRoot xml; Flags = parseFlagsList xml }

module Sources =
  type ParserType =
    | Provides
    | Requires

  let private parse parserType source =
    let pattern = 
      match parserType with
        | Provides -> @"^\s*goog\.provide\(\s*[\'""]([^\)]+)[\'""]\s*\)"
        | Requires -> @"^\s*goog\.require\(\s*[\'""]([^\)]+)[\'""]\s*\)"

    Regex.Matches(source, pattern, RegexOptions.Multiline)
      |> Seq.cast<Match>
      |> Seq.map (fun m -> m.Groups.[1].Value)
      |> Seq.toList

  let parseSource file =
    let source = File.ReadAllText file
    let provides = parse Provides source
    let requires = parse Requires source
    { Path = file; Provides = provides; Requires = requires; Text = source }

let getClosureBaseFile sources =
  let isBaseFile (p, s) = Path.GetFileName p = "base.js"
  let hasGoogDefinition (l: string) = l.StartsWith "var goog = goog || {};"
  sources
    |> Seq.map (fun s -> (s.Path, s))
    |> Seq.filter isBaseFile
    |> Seq.collect (fun (p, s) ->  s.Text.Split '\n' |> PSeq.filter hasGoogDefinition |> PSeq.map (fun _ -> p))
    |> Seq.head

let rec resolveDependencies ns (depsList: System.Collections.Generic.List<string>) (providesMap: Map<string, Source>) traversalPath =
  if not (providesMap.ContainsKey ns) then printf "Could not find required namespace %s." ns
  if traversalPath |> List.exists (fun x -> x = ns) then printf "Circular dependency on %s." ns
  let source = providesMap.[ns]
  if depsList.Contains source.Path then depsList
  else
    for reqNs in source.Requires do
      resolveDependencies reqNs depsList providesMap (ns :: traversalPath) |> ignore
    depsList.Add(source.Path)
    depsList

let getFilesFlags ns sources providesMap =
  let baseFile = [ getClosureBaseFile sources ]
  let list = new System.Collections.Generic.List<string>()
  let dependencies = resolveDependencies ns list providesMap [] |> Seq.toList
  baseFile @ dependencies
    |> Seq.map (fun s -> sprintf "--js=%s" s)
    |> Seq.toList

let writeFlagsFile sources providesMap output =
  let fileName = output.File
  let filesFlags = getFilesFlags output.Namespace sources providesMap
  let content = output.Flags @ filesFlags |> String.concat Environment.NewLine
  File.WriteAllText(fileName, content, Encoding.ASCII)

let getProvidesMap sources =
  let provides = sources |> Seq.collect (fun s -> s.Provides |> Seq.map (fun p -> (s, p)))
  let mutable dict = Map.empty<string, Source>
  for s, p in provides do
    if dict.ContainsKey p then printf "Multiple provide of %s in files %O, %O" p dict.[p] s
    else dict <- dict.Add(p, s)
  dict

let ensureRequiredNamespaces sources (providesMap: Map<string, Source>) =
  sources
    |> Seq.collect (fun s -> s.Requires)
    |> Seq.filter (fun r -> not (providesMap.ContainsKey r))
    |> Seq.iter (fun r -> printf "Missing required namespace %O" r)

let getCommonFlags flags =
  flags
    |> Seq.filter (fun f -> f.Namespaces.IsEmpty)
    |> Seq.collect (fun f -> f.Flags)
    |> Seq.toList

let getNsFlags ns flags =
  flags
    |> Seq.filter (fun f -> f.Namespaces |> Seq.exists (fun n -> n = ns))
    |> Seq.collect (fun f -> f.Flags)
    |> Seq.toList

let writeFlagsFiles options sources providesMap =
  let flags = options.Flags
  let namespaces = flags |> Seq.collect (fun f -> f.Namespaces) |> Seq.toList
  let commonFlags = getCommonFlags flags
  for ns in namespaces do
    let nsFlags = flags |> getNsFlags ns
    let fileName = Path.Combine(options.Root, sprintf "%s.txt" ns)
    writeFlagsFile sources providesMap { Namespace = ns; Flags = commonFlags @ nsFlags; File = fileName }

let getFilesRec root = Directory.GetFiles(root, "*.js", SearchOption.AllDirectories)

let main (args: string array) =
  let inputFile = args.[1]
  let options = Options.parse inputFile
  let sources = getFilesRec options.Root |> Seq.sort |> PSeq.map Sources.parseSource |> PSeq.toList
  let providesMap = getProvidesMap sources
  ensureRequiredNamespaces sources providesMap
  writeFlagsFiles options sources providesMap

main (Environment.GetCommandLineArgs())
(*
<linker>
  <root>c:\projects\sitepolis\trunk\src\SmartWeb.Web\Assets\js\</root>
  <flags>
    --some
    --flag
  </flags>
  <flags namespaces="edit2">
   --only-for-edit2
  </flags>
  <flags namespaces="site;admin;client">
    --for-these
  </flags>
</linker>
*)