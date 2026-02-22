using System.Xml;
using System.Xml.Schema;

namespace JXSchemaGenerator.Schema;

public sealed class SchemaIndex
{
	public Dictionary<XmlQualifiedName, XmlSchemaComplexType> ComplexTypes { get; } = new();
	public Dictionary<XmlQualifiedName, XmlSchemaElement> GlobalElements { get; } = new();
	public Dictionary<XmlQualifiedName, XmlSchemaSimpleType> SimpleTypes { get; } = new();

	public static SchemaIndex Build(XmlSchemaSet set)
	{
		var index = new SchemaIndex();

		foreach (XmlSchema schema in set.Schemas())
		{
			foreach (XmlSchemaObject item in schema.Items)
			{
				switch (item)
				{
					case XmlSchemaComplexType ct when !ct.QualifiedName.IsEmpty:
						index.ComplexTypes[ct.QualifiedName] = ct;
						break;
					case XmlSchemaSimpleType st when !st.QualifiedName.IsEmpty:
						index.SimpleTypes[st.QualifiedName] = st;
						break;
					case XmlSchemaElement el when !el.QualifiedName.IsEmpty:
						index.GlobalElements[el.QualifiedName] = el;
						break;
				}
			}
		}

		return index;
	}

	public SchemaIndexDump ToDump(int maxNames = 200)
	{
		var ctNames = new List<string>(maxNames);
		foreach (var k in ComplexTypes.Keys.OrderBy(k => k.Name))
		{
			if (ctNames.Count >= maxNames) break;
			ctNames.Add($"{k.Namespace}|{k.Name}");
		}

		var elNames = new List<string>(maxNames);
		foreach (var k in GlobalElements.Keys.OrderBy(k => k.Name))
		{
			if (elNames.Count >= maxNames) break;
			elNames.Add($"{k.Namespace}|{k.Name}");
		}

		return new SchemaIndexDump(
			ComplexTypes.Count,
			SimpleTypes.Count,
			GlobalElements.Count,
			ctNames,
			elNames
		);
	}

	public XmlSchemaComplexType GetComplexTypeOrThrow(XmlQualifiedName name) => ComplexTypes.TryGetValue(name, out var ct) ? ct : throw new KeyNotFoundException($"ComplexType not found: {name}");

	public XmlSchemaElement GetElementOrThrow(XmlQualifiedName name) => GlobalElements.TryGetValue(name, out var el) ? el : throw new KeyNotFoundException($"GlobalElement not found: {name}");

	public override string ToString() => $"SchemaIndex: ComplexTypes={ComplexTypes.Count}, SimpleTypes={SimpleTypes.Count}, GlobalElements={GlobalElements.Count}";
}
