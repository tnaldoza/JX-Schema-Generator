# Output format 2

Each Master XSD compiles to one JSON file that describes every operation in the Master, the element tree of its request and response, and a table of the named types those trees use.
`formatVersion` is `2`.
The JSON Schema `jxchange-schemas.schema.json` describes the format and marks the keys that are always written.

Examples quote a hand-written sample Master with invented text.

## Files and naming

A run writes one file per Master, named from the Master file name:

- The optional `TPG_` prefix and the `Master.xsd` suffix are removed without regard to case.
- An all-uppercase stem such as `ACH` is lowercased whole, so `TPG_ACHMaster.xsd` becomes `ach.json`.
- Any other stem loses the case of its first character only, so `TPG_BillPayMaster.xsd` becomes `billPay.json`.

The run also writes `jxchange-schemas.schema.json` into the same folder, and every Master file points at it through its `$schema` key.
The release bundle `jxchange-schemas.zip` contains the schema file next to the Master files.
A client that loads every `*.json` file in the folder must skip the schema file.

Files are UTF-8 without a byte order mark, indented with two spaces, with `\n` line endings and one trailing newline.
`&` and non-ASCII characters are written as they are, while `"` and `\` are escaped.

## Document keys

| Key             | Meaning                                                                          |
| --------------- | -------------------------------------------------------------------------------- |
| `$schema`       | `./jxchange-schemas.schema.json`                                                 |
| `formatVersion` | `2`                                                                              |
| `tpgVersion`    | The `version` attribute of the Master's `xsd:schema`, or `null` when it has none |
| `master`        | The Master's XSD file name                                                       |
| `operations`    | Operations keyed by operation name, in ordinal order                             |
| `fault`         | Element nodes of the SOAP fault; see [Fault](#fault)                             |
| `types`         | Named types keyed by local name, in ordinal order                                |

An operation name is the name of the request type without its `Rq_MType` suffix.
`operations`, `fault` and `types` are always written.

## Operation keys

| Key                | Meaning                                                                                       |
| ------------------ | --------------------------------------------------------------------------------------------- |
| `kind`             | `inquiry`, `search`, `add`, `modify`, `validate` or `other`, taken from the operation name    |
| `requestElement`   | The global request element, which can differ from the operation name                          |
| `responseElement`  | The global response element, omitted when the Master defines none                             |
| `annotation`       | The documentation of the request element, or of the request type when the element has none    |
| `extendedElements` | See [Derived helpers](#derived-helpers)                                                       |
| `records`          | See [Derived helpers](#derived-helpers)                                                       |
| `modify`           | See [Derived helpers](#derived-helpers)                                                       |
| `request`          | Element nodes of the request, always written, and `{}` when the request has no elements       |
| `response`         | Element nodes of the response, or `null` when the operation has no response, always written   |

The kind comes from the end of the operation name: `Validate`, `Inq`, `Srch`, `Add` or `Mod` selects the matching kind, and any other name is `other`.
A `validate` operation wraps the request of the operation it validates as a child element, so its tree repeats that request.

## Element nodes

`request` and `response` are objects keyed by element name.
Each value is a node, whose keys starting with `$` are metadata and whose other keys are child elements in XSD sequence order.
A client must treat every `$`-prefixed key as metadata and never as a child element.

| Key           | Meaning                                                                                |
| ------------- | -------------------------------------------------------------------------------------- |
| `$type`       | The built-in XSD type of a leaf, such as `string`, `decimal`, `date` or `base64Binary` |
| `$typeName`   | The named type, a key of `types`; omitted for built-in and anonymous types             |
| `$required`   | `true` when the element is required; written in requests only                          |
| `$array`      | `true` when the element repeats                                                        |
| `$maxOccurs`  | On every repeating element: the most occurrences allowed, or `null` when there is no limit |
| `$recursive`  | `true` when the element's type is already being expanded above it                      |
| `$attrs`      | Attribute names, in ordinal order                                                      |
| `$annotation` | The element's documentation                                                            |

Element nodes, `request`, `operations` and `types` are always written, so an element node can be `{}`.
`$maxOccurs` is written exactly when `$array` is, and its `null` means no limit.
All other keys are omitted when their value is false or empty.

`$type` and `base` are open strings.
The XSDs already use `anyURI` and `integer` besides `string`, `decimal`, `int`, `long`, `date`, `dateTime`, `time` and `base64Binary`, and a client must not treat the list as closed.

### `$required`

`$required` appears in requests only and is omitted in responses.
It is relative to the parent: an element is required when its own `minOccurs` is at least 1 and the sequences and group references that hold it inside the parent's type are required too.
It says nothing about whether the parent is present, so `EmailInfo` can carry `$required` inside an optional `EmailArray`.
An element without `$required` is optional.

### `$array`

`$array` sits on the repeating element, not on the wrapper that contains it.
In the sample, `EmailArray` is the wrapper and `EmailInfo` is the item that repeats:

```json
"EmailArray": {
  "$typeName": "EmailArray_AType",
  "EmailInfo": {
    "$typeName": "EmailInfo_CType",
    "$required": true,
    "$array": true,
    "$maxOccurs": null,
    "EmailAddr": { "$type": "string", "$typeName": "Name_Type", "$attrs": ["JHANull", "MaskVal", "SrchType"] }
  }
}
```

A client serializes `EmailArray` as one object holding a list of `EmailInfo` items.
`EmailInfo` has no limit, while an element declared with `maxOccurs="3"` carries `"$maxOccurs": 3`, and a client must not send more items than that.

### `$recursive`

When an element's type is already being expanded further up the same path, the node carries `$recursive: true` and no children.
The type's content is found in the enclosing node that uses the same type.
In the sample, `QuesArray` appears again inside `QuesRec`:

```json
"QuesRec": {
  "$typeName": "QuesRec_CType",
  "$array": true,
  "$maxOccurs": null,
  "QuesText": { "$type": "string", "$typeName": "Name_Type", "$attrs": ["JHANull", "MaskVal", "SrchType"] },
  "QuesArray": { "$typeName": "QuesArray_AType", "$recursive": true }
}
```

Each recursion also raises a warning during the run.
Elements nested deeper than 64 levels fail the Master.

## Annotations

`$annotation` on a node and `annotation` on an operation share one layout.
The keys are omitted when empty, and the whole object is omitted when nothing is left.

| Key           | Meaning                                                                                               |
| ------------- | ----------------------------------------------------------------------------------------------------- |
| `description` | The element description with whitespace collapsed to single spaces                                    |
| `values`      | Known values listed in the documentation                                                              |
| `default`     | The documented default value                                                                          |
| `deprecated`  | `{ "date", "comment" }`, where either field is omitted when absent                                    |
| `providers`   | Provider-specific blocks as `{ "Provider": { "Child": "text" } }`, with providers and children sorted |
| `notes`       | Other documentation fields as `{ "Field": "text" }`, sorted                                           |

A `<Deprecate>` element with no date and no comment is written as `"deprecated": {}`, because its presence is the deprecation fact.

### How documentation is read

- Element names are matched without regard to case, so `<jx>` counts as `<Jx>`.
- `description` joins, in document order, the plain text of the documentation, the text of each `ElemDesc` field and the text a `Jx` element holds outside its child elements.
- `values` comes from the value fields, split at commas, and `default` from the first default field.
  The XSDs spell these fields more than one way, and every spelling counts:

  | Field     | Spellings                                                                       |
  | --------- | ------------------------------------------------------------------------------- |
  | `values`  | `CanonicalVal`, `CanonVal`                                                      |
  | `default` | `ElemDefault`, `ElemDft`, `ElemDef`, `Dft`, `Default`, `Defalut`, `Defalult`    |

- `deprecated` comes from the first `Deprecate` element.
- These fields count both inside a `Jx` element and as top-level elements of the documentation.
- Any other field inside `Jx`, and any other top-level element without child elements, is written to `notes` under its element name, and the first one of a name wins.
  A provider name used as a top-level element without child elements, such as `<Synergy>text</Synergy>`, is therefore a note.
- A top-level element with child elements, other than `Jx` and `Deprecate`, is a provider block, and its child elements are its fields.
- Keys in `notes` and `providers` keep the spelling of the XSD, except the names in [Provider names](#provider-names) and `ElemCanocType`, which is written as `ElemCanonType`.
- Text that is empty after whitespace is collapsed is never written, and a provider block without a non-empty field is left out.

## Type entries

`types` lists every named type that an element node, an attribute or a base chain refers to, as an object keyed by local name.
Two types with the same local name in different namespaces fail the Master.
A type entry carries the annotation fields flattened, so there is no nested `annotation` object.

| Key            | Meaning                                                                                           |
| -------------- | ------------------------------------------------------------------------------------------------- |
| `base`         | The built-in type at the end of the base chain, such as `string` or `decimal`                     |
| `baseTypeName` | The direct base type, a key of `types`                                                            |
| `enum`         | `closed` or `open`; see [Enumerations](#enumerations)                                             |
| `values`       | The nearest non-empty list of known values along the base chain, starting at the type itself      |
| `description`  | As in an annotation                                                                               |
| `default`      | As in an annotation                                                                               |
| `deprecated`   | As in an annotation                                                                               |
| `providers`    | As in an annotation                                                                               |
| `notes`        | As in an annotation                                                                               |
| `attributes`   | Attributes as `{ "Name": { "type", "typeName", "required" } }`; `required` is written only when `true` |

Complex types with nothing to report are written as `{}`.
An attribute's `type` is its built-in type and `typeName` its named type, each omitted when it does not apply.
The sample's `AcctType_Type`:

```json
"AcctType_Type": {
  "base": "string",
  "baseTypeName": "AcctTypeBase_Type",
  "enum": "closed",
  "values": ["D", "L", "S"],
  "attributes": {
    "JHANull": { "type": "string", "typeName": "JHANull_Type" },
    "Rstr": { "type": "string", "typeName": "Rstr_Type" }
  }
}
```

## Enumerations

A type is `closed` when `ClosedEnum_Type` is in its base chain, and `open` when `OpenEnum_Type` is in its base chain or the chain lists values without either marker.
A type with neither carries no `enum` key.

- Only `closed` lists are safe to turn into a closed union or enum, and only for requests.
- A `closed` type can have no `values` when its documentation lists none, and a client keeps such a type a plain `string`.
- `open` lists are known values, and the institution can use others.
- Responses can return a value that is on no list, so a client must not reject one.

## Provider names

The XSDs spell some provider names more than one way, such as `Silverlake` and `SilverLake`.
These names are matched without regard to case and always written with one spelling, both as `providers` keys and as `notes` keys:

`CIF2020`, `CoreDirector`, `Episys`, `RSA`, `Silhouette`, `SilverLake`, `Synapsys` and `Synergy`.

Any other provider name keeps the spelling of the XSD.
A run warns when two provider names in a Master differ only by case, so a new one can be added to the list.
`Synergy_CU` is a separate block for credit unions, not another spelling of `Synergy`.
In a provider block, `ElemSPMap` with the text `NA` marks an element that the provider does not support.

## Fault

When a call fails, jXchange answers with a SOAP fault whose detail holds one `HdrFault` element.
`fault` holds the element nodes of `HdrFault`'s content, built the same way as a `response`, and its types are in `types`.
It is `null` when the Master declares no `HdrFault` element, and the run then warns.
In the TPG XSDs, `HdrFault` is the same in every Master:

- `FaultHdr` sends header details back, such as `AuditUsrId`, `ConsumerName` and `jXLogTrackingId`.
- `FaultRecInfoArray` holds a repeating `FaultMsgRec` with `ErrCode`, `ErrCat`, `ErrDesc`, `ErrElem`, `ErrElemVal` and `ErrLoc`.

## What the output leaves out

- Message headers: `MsgRqHdr`, `SrchMsgRqHdr`, `MsgRsHdr` and `SrchMsgRsHdr` are not written.
- `Ver_` markers: an element named `Ver_` followed by digits is never written, in requests, responses or the fault, and any elements its type declares appear in the parent.
  The services do not require the markers, so no key records which version added an element.
- `xsd:any` extension points: no key stands for a wildcard.

Every element can carry children that the file does not list.
Responses can omit any element, and a client keeps the elements it does not know.

A construct outside the supported XSD subset fails the Master with an error that names the type or element.
The unsupported constructs are `xsd:choice`, `xsd:all`, a repeating sequence or group reference, complexContent restriction and an anonymous complex type with attributes.

## Derived helpers

The helpers are computed from the element trees and the XSD structure.
No source file lists operation, container or Master names.

### `extendedElements`

`extendedElements` lists the distinct `x_` element names found in the response, in ordinal order, when the request has a top-level `IncXtendElemArray`.
The search does not descend into an `x_` element, so an `x_` element nested inside another is not listed.
The key is omitted when the request has no `IncXtendElemArray`, when the response is `null`, or when no `x_` element exists.
A client puts these names into `IncXtendElemArray.IncXtendElemInfo.XtendElem`, whose node is a plain `string`.

### `records`

`records` is written for `search` operations only, and lists where the response holds the rows the search found.
A repeating element directly under the response root is listed by name, and a repeating child of a root element is listed as `Parent/Name`.
When neither level holds a repeating element, `records` lists the shallowest repeating elements anywhere in the response instead, each as its full path from the root, such as `SrchInfo/RecArray/Rec`.
Two kinds of element never hold records:

- A response root element with the same name as a request root element is the request's search criteria sent back, so neither it nor anything inside it is a record.
- An `x_` element is an optional extra, so neither it nor anything inside it is a record.

Paths are in XSD order, joined with `/`, and point into the full `response`.
In the sample, `AcctSrch` sends a `FilterArray` and gets it back next to its results, so only the results are listed:

```json
"records": ["AcctSrchRecArray/AcctSrchRec"]
```

`records` is omitted when no record is found, and the run then warns `Search <name> has no repeating record in its response`.

### `modify.containers`

`modify` is written for `modify` operations only.
It holds `containers`, an array in which each container has `name` and `sections`:

- A container is a complex root child of the request whose name ends in `Mod`.
  `name` is that element name and `sections` lists its complex children in XSD order, leaving out `Custom`.
- When the request has no such child, the operation has one container with `name: null`, and `sections` lists the complex root children that are not envelope elements.
- A container can have `"sections": []`.

The envelope elements are the root children shared by many operations that are not sections of any one of them:

`ErrOvrRdInfoArray`, `AccountId`, `Dlt`, `Custom`, `ActIntentKey`, `ActIntent` and `ModRmkArray`.

`ModRmkArray` is a remark on the modification, not a record section.
A run warns when another complex element sits at the root of most modify requests in a Master but is missing from this list.
Simple-typed root children are record keys, never sections, so they are not counted.

## Stability

Output is byte-identical for the same XSDs: every dictionary and every sort uses ordinal comparison, and element children keep XSD sequence order.
`formatVersion` changes only with a breaking change to the keys, their order or their meaning.
A client ignores keys it does not know, so a new optional key is not breaking.

## Running the generator

`generate --input <folder> [--root <file>] [--out <folder>]` writes the files described above.

| Option    | Meaning                                                                                   |
| --------- | ----------------------------------------------------------------------------------------- |
| `--input` | Folder holding the Masters and every XSD they include                                     |
| `--root`  | One Master, as a bare file name inside `--input`; a path with a folder part fails the run |
| `--out`   | Output folder, `artifacts` by default                                                     |

| Exit code | Meaning                                                             |
| --------- | ------------------------------------------------------------------- |
| 0         | Every Master was written                                            |
| 1         | A Master failed, no Master was found, or `--root` has a folder part |
| 2         | Usage error                                                         |

A Master that fails does not stop the others.
The run never deletes files, and it warns about each other `*.json` file in the output folder that it did not write.
A `--root` run into a folder that holds a full run's output therefore warns that the files of the other Masters are stale.
