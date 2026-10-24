# Migrating to format 2

Format 2 replaces the four per-verb files of each Master with one file per Master.
The full key reference is in [format.md](format.md).
This guide maps each old key to its new place, for the clients that read the old files: JXChange-CS, JXChange-TS and the TypeScript type generator `generate-types.js`.

## Files

The `*.json` files in the folder are no longer all Masters.
`jxchange-schemas.schema.json` sits next to them and ships in `jxchange-schemas.zip`, so a client that loads every `*.json` file must skip it.

Old files were named `<stem>InquiryElements.json`, `<stem>AddElements.json`, `<stem>SearchElements.json` and `<stem>ModificationElements.json`, and a Master had only the files for the verbs it supports.
The new file is `<stem>.json`, and `<stem>` follows the naming rule in [format.md](format.md#files-and-naming).

| New file | Old files |
| --- | --- |
| `ach.json` | `ach` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `billPay.json` | `billPay` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `brdCst.json` | `brdCstSearchElements.json` |
| `crCard.json` | `crCard` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `custom.json` | `custom` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `customer.json` | `customer` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `deposit.json` | `deposit` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `ens.json` | `ens` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `image.json` | `image` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `ims.json` | `ims` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `inquiry.json` | `inquiryElements.json` and `inquirySearchElements.json` |
| `loan.json` | `loan` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `report.json` | None, because the old generator wrote no file for `TPG_ReportMaster.xsd` |
| `teller.json` | `teller` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `transaction.json` | `transactionAddElements.json` and `transactionModificationElements.json` |
| `wire.json` | `wire` followed by `InquiryElements.json`, `AddElements.json`, `SearchElements.json` and `ModificationElements.json` |
| `workflow.json` | `workflowSearchElements.json` |

A Master that is not in this table gets a file name by the same rule, because no source file lists Master names.
Old files left in an output folder are not deleted by the generator, which warns about each one.
Delete them by hand.

## Keys

| Old                                                               | New                                                                    |
| ----------------------------------------------------------------- | ---------------------------------------------------------------------- |
| Four files per Master                                             | One `<stem>.json` per Master                                           |
| `version`                                                         | `formatVersion`, a number that is `2`                                  |
| `lastUpdated`                                                     | Removed with no replacement; release notes carry the date              |
| None                                                              | `tpgVersion`, the Master's `version` attribute or `null`               |
| `inquiryOperations.<Op>`, `searchOperations.<Op>`, `addOperations.<Op>` | `operations.<Op>` with `kind` set to `inquiry`, `search` or `add` |
| `<Op>.name`                                                       | The key `<Op>` itself, and `requestElement` for the element name       |
| `requestSchema`                                                   | `request`                                                              |
| `responseSchema`                                                  | `response`, or `null` when the operation has no response               |
| `responseRecordSchema`                                            | `records`, a list of paths into the full `response`                    |
| `null` leaf                                                       | An object with `$type`, plus `$typeName` when the type is named        |
| `isArray: true` on a wrapper                                      | `$array: true` on the repeating child                                  |
| None                                                              | `$maxOccurs` on the repeating child, `null` when there is no limit     |
| None                                                              | `fault`, the element nodes of the SOAP fault's `HdrFault`              |
| Patched `XtendElem` list in `requestSchema`                       | `extendedElements`; the `XtendElem` node is a plain `string`           |
| `entityTypes.<Key>`                                               | `modify.containers`, an array whose items carry the container `name`   |
| `entityTypes.<Key>.modificationSections[].name`                   | `modify.containers[].sections`                                         |
| `entityTypes.<Key>.modificationSections[].schema`                 | The container's children under `request.<Container>`                   |
| `entityTypes.<Key>.name`                                          | `types.<typeName>.description`                                         |
| `entityTypes.<Key>.aliases`                                       | No equivalent; see [Account codes](#account-codes)                     |

New operations exist that the old files never had: `validate` operations, `modify` operations of every Master, and operations of kind `other`.
An operation key is the request type name without `Rq_MType`, and `requestElement` can differ from it.

### Records

A search operation's `records` lists the repeating elements of its `response`, in place of the separate `responseRecordSchema` tree.
Each path runs from the response root with `/` between names: `Name` for a repeating element under the root, `Parent/Name` for one level below a root child, and a longer path when a search's records sit deeper.
A client reads the record's fields from `response`, following the same path, and sees the record's full tree including `x_` elements.
Search criteria that the response sends back, such as a filter list the request also carries, and `x_` elements are never listed as records.

### Leaves and attributes

A leaf was `null`.
It is now an object whose `$` keys describe it, and the client reads the leaf type from `$type` and the named type from `$typeName`.
Attributes were not in the old files.
Names are in `$attrs`, and their types are in `types.<typeName>.attributes`.

### Arrays

An old file marked the wrapper, for example `EmailArray`, with `isArray: true`, and its single child was the item.
Format 2 puts `$array: true` on the item, `EmailInfo`.
The wrapper has no marker, and a client that emits `EmailArray` as one object holding a list reads the item's `$array`.
The item also carries `$maxOccurs`, the most items a request may send, or `null` when there is no limit.

### Extended elements

The old inquiry file replaced the contents of `IncXtendElemArray.IncXtendElemInfo.XtendElem` with a list of names.
Format 2 leaves `XtendElem` as a `string` node and lists the names in the operation's `extendedElements`.

### Modification

An old modification file held `entityTypes`, keyed by an account code or a request type name, with one list of sections per entity.
Format 2 keys nothing by code.
An operation of kind `modify` has `modify.containers`:

- Each container is named by the `*Mod` element that holds its sections in the request, such as `DepMod`.
- A request without a `*Mod` element has one container with `name: null`, and its sections are the request's root children.
- The section names are in `sections`, and each section's tree is under `request.<Container>.<Section>`, or under `request.<Section>` when `name` is `null`.

`ModRmkArray` is an envelope element and never a section.

## Account codes

The XSDs do not carry the codes and aliases that the old `ModificationDomainConfig.cs` assigned, so a client that needs them keeps this table itself:

| Code | Aliases | Operation   | Container    |
| ---- | ------- | ----------- | ------------ |
| `D`  | `S`, `X` | `AcctMod`  | `DepMod`     |
| `T`  |         | `AcctMod`   | `TimeDepMod` |
| `B`  |         | `AcctMod`   | `SafeDepMod` |
| `C`  |         | `AcctMod`   | `TrckMod`    |
| `L`  | `O`     | `LnAcctMod` | `LnMod`      |

All five rows are in `deposit.json` and `loan.json`.
`AcctMod` in `deposit.json` also has an `LnMod` container, which the old file left out.

Old display names such as `Deposit/Checking/Savings` were also written by hand.
Format 2 offers `types.<typeName>.description`, taken from the XSD, for a type that has documentation, where `<typeName>` is the container's `$typeName`.

## Type generation

For `generate-types.js`:

- Read the leaf type from `$type`, and the named type from `$typeName` for a union or enum.
- Read `$array` on the item to decide that a property is a list, and `$maxOccurs` for its limit, where `null` means none.
- Treat every key starting with `$` as metadata and never as a child element.
- Read `$required` in requests only, and treat a missing `$required` as optional.
- Turn `types.<name>.values` into a union only when `enum` is `closed`, and only for request types.
  Keep `open` lists, and `closed` types without `values`, as `string`.
- Expect `$type` and `base` values beyond the common eight, such as `anyURI` and `integer`.
- Stop at a node with `$recursive: true`.
- Build the SOAP fault type from `fault` the same way as a response.
- Skip `jxchange-schemas.schema.json` when listing files.

The old per-verb entry points map to `kind`:

- Inquiry clients read operations with `kind: "inquiry"` and their `extendedElements`.
- Search clients read `kind: "search"` and `records`.
- Add clients read `kind: "add"`.
- Modification clients read `kind: "modify"` and `modify.containers`.
