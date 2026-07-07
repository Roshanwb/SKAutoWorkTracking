# GenerateResx.ps1
# Reads StringKeyMapping.csv and generates Strings.resx and Strings.fr.resx

$csvPath = "StringKeyMapping.csv"
$resxEnPath = "Localization/Strings.resx"
$resxFrPath = "Localization/Strings.fr.resx"

# Read the mapping
$mappings = Import-Csv $csvPath

# English ResX
$xmlEn = '<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'

# Add data entries
$body = ""
foreach ($m in $mappings) {
    $key = $m.Key
    $value = $m.RawString -replace '"', ''
    $body += "  <data name=`"$key`" xml:space=`"preserve`">`n"
    $body += "    <value>$value</value>`n"
    $body += "  </data>`n"
}

$xmlEn += $body
$xmlEn += '</root>'

# French ResX (for now, copy English values – you'll translate later)
$xmlFr = $xmlEn

# Write files
$xmlEn | Out-File $resxEnPath -Encoding utf8
$xmlFr | Out-File $resxFrPath -Encoding utf8

Write-Host "Generated $resxEnPath and $resxFrPath with $($mappings.Count) entries."