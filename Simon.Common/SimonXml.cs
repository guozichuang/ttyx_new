using System;
using System.Collections.Generic;
using System.Text;

using System.Xml;
using System.IO;

namespace Simon.Common
{
    /// <summary>
    /// XML操作辅助类
    /// </summary>
    public class SimonXml
    {
        public static XmlNode AppendElement(XmlNode node, string newElementName)
        {
            return AppendElement(node, newElementName, null);
        }

        public static XmlNode AppendElement(XmlNode node, string newElementName, string innerValue)
        {
            XmlNode oNode;

            if (node is XmlDocument)
                oNode = node.AppendChild(((XmlDocument)node).CreateElement(newElementName));
            else
                oNode = node.AppendChild(node.OwnerDocument.CreateElement(newElementName));

            if (innerValue != null)
                oNode.AppendChild(node.OwnerDocument.CreateTextNode(innerValue));

            return oNode;
        }

        /// <summary>
        /// 创建Attribute
        /// </summary>
        /// <param name="xmlDocument">xml文档</param>
        /// <param name="name">Attribute名</param>
        /// <param name="value">Attribute值</param>
        /// <returns></returns>
        public static XmlAttribute CreateAttribute(XmlDocument xmlDocument, string name, string value)
        {
            XmlAttribute oAtt = xmlDocument.CreateAttribute(name);
            oAtt.Value = value;
            return oAtt;
        }

        /// <summary>
        /// 设置Attribute
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="attributeName">Attribute名</param>
        /// <param name="attributeValue">Attribute值</param>
        public static void SetAttribute(XmlNode node, string attributeName, string attributeValue)
        {
            if (node.Attributes[attributeName] != null)
                node.Attributes[attributeName].Value = attributeValue;
            else
                node.Attributes.Append(CreateAttribute(node.OwnerDocument, attributeName, attributeValue));
        }

        /// <summary>
        /// 格式化xml
        /// </summary>
        /// <param name="xml">xml</param>
        /// <returns>格式化后的xml</returns>
        public static string FormatXml(XmlDocument xml)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            XmlTextWriter xw = new XmlTextWriter(new StringWriter(sb));
            xw.Formatting = Formatting.Indented;
            xw.Indentation = 1;
            xw.IndentChar = '\t';
            xml.WriteTo(xw);
            return sb.ToString();
        }
    }
}
