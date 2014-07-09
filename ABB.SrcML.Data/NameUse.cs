﻿/******************************************************************************
 * Copyright (c) 2014 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *    Vinay Augustine (ABB Group) - initial API, implementation, & documentation
 *    Patrick Francis (ABB Group) - API, implementation, & documentation
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml;

namespace ABB.SrcML.Data {
    /// <summary>
    /// Represents the generalized use of a name. This does not distinguish whether the name represents a type, or variable, or what.
    /// </summary>
    public class NameUse : Expression {
        private NamePrefix prefix;
        private List<Tuple<Expression, string>> aliases;
        
        /// <summary> The XML name for NameUse </summary>
        public new const string XmlName = "n";

        /// <summary> XML Name for <see cref="Name" /> </summary>
        public const string XmlNameName = "val";

        /// <summary> XML Name for <see cref="Prefix" /> </summary>
        public const string XmlPrefixName = "Prefix";

        /// <summary>
        /// The binary operators that indicate that the name on the right-hand side is a child of the left-hand side.
        /// </summary>
        protected static readonly string[] NameInclusionOperators = {".", "->", "::"};

        /// <summary> The name being used. </summary>
        public string Name { get; set; }

        /// <summary>
        /// The prefix of the name. In a fully-qualified name like System.IO.File, the name is File and the prefix is System.IO.
        /// </summary>
        public NamePrefix Prefix {
            get { return prefix; }
            set {
                prefix = value;
                if(prefix != null) {
                    prefix.ParentExpression = this;
                    prefix.ParentStatement = this.ParentStatement;
                }
            }
        }

        /// <summary> The statement containing this expression. </summary>
        public override Statement ParentStatement {
            get { return base.ParentStatement; }
            set {
                base.ParentStatement = value;
                if(Prefix != null) { Prefix.ParentStatement = value; }
            }
        }

        /// <summary>
        /// Determines the set of aliases/imports active at the site of this name use.
        /// </summary>
        /// <returns>A list of tuples describing each alias. Each tuple contains (1) the target and (2) the new name for it, if any.</returns>
        public IList<Tuple<Expression, string>> GetAliases() {
            if(aliases == null) {
                //alias list not yet initialized
                //search through prior statements for imports/aliases
                aliases = new List<Tuple<Expression, string>>();
                var currentStmt = this.ParentStatement;
                while(currentStmt != null) {
                    foreach(var sibling in currentStmt.GetSiblingsBeforeSelf()) {
                        if(sibling is ImportStatement) {
                            var import = sibling as ImportStatement;
                            aliases.Add(new Tuple<Expression, string>(import.ImportedNamespace, null));
                        } else if(sibling is AliasStatement) {
                            var alias = sibling as AliasStatement;
                            aliases.Add(new Tuple<Expression, string>(alias.Target, alias.AliasName));
                        }
                    }
                    currentStmt = currentStmt.ParentStatement;
                }
            }
            return aliases;
        }

        /// <summary>
        /// Instance method for getting <see cref="NameUse.XmlName"/>
        /// </summary>
        /// <returns>Returns the XML name for NameUse</returns>
        public override string GetXmlName() { return NameUse.XmlName; }

        /// <summary>
        /// Processes the child of the current reader position into a child of this object.
        /// </summary>
        /// <param name="reader">The XML reader</param>
        protected override void ReadXmlChild(XmlReader reader) {
            if(XmlPrefixName == reader.Name) {
                Prefix = XmlSerialization.ReadChildExpression(reader) as NamePrefix;
            } else {
                base.ReadXmlChild(reader);
            }
        }

        /// <summary>
        /// Read the XML attributes from the current <paramref name="reader"/> position
        /// </summary>
        /// <param name="reader">The XML reader</param>
        protected override void ReadXmlAttributes(XmlReader reader) {
            string attribute = reader.GetAttribute(XmlNameName);
            if(!String.IsNullOrEmpty(attribute)) {
                Name = attribute;
            }
            base.ReadXmlAttributes(reader);
        }

        /// <summary>
        /// Writes the contents of this object to <paramref name="writer"/>.
        /// </summary>
        /// <param name="writer">The XML writer to write to</param>
        protected override void WriteXmlContents(XmlWriter writer) {
            if(null != Prefix) {
                XmlSerialization.WriteElement(writer, Prefix, XmlPrefixName);
            }
            base.WriteXmlContents(writer);
        }

        /// <summary>
        /// Writes XML attributes from this object to the XML writer
        /// </summary>
        /// <param name="writer">The XML writer</param>
        protected override void WriteXmlAttributes(XmlWriter writer) {
            writer.WriteAttributeString(XmlNameName, Name);
            base.WriteXmlAttributes(writer);
        }

        /// <summary> Returns a string representation of this object. </summary>
        public override string ToString() {
            return string.Format("{0}{1}", Prefix, Name);
        }

        /// <summary>
        /// Finds definitions that match this name.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<INamedEntity> FindMatches() {
            if(ParentStatement == null) {
                throw new InvalidOperationException("ParentStatement is null");
            }

            //handle keywords
            if(Name == "this" ||
               (Name == "base" && ProgrammingLanguage == Language.CSharp) ||
               (Name == "super" && ProgrammingLanguage == Language.Java)) {
                return TypeDefinition.GetTypeForKeyword(this);
            }


            //TODO: search for variable declarations
            //search for types/namespaces/methods/properties

            //If there's a prefix, resolve that and search under results
            if(Prefix != null) {
                return Prefix.FindMatches().SelectMany(ns => ns.GetNamedChildren<TypeDefinition>(this.Name));
            }

            //If preceded by a name, match and search under results
            var siblings = GetSiblingsBeforeSelf().ToList();
            var priorOp = siblings.LastOrDefault() as OperatorUse;
            if(priorOp != null && NameInclusionOperators.Contains(priorOp.Text)) {
                var priorName = siblings[siblings.Count - 2] as NameUse; //second-to-last sibling
                if(priorName != null) {
                    var parents = priorName.FindMatches();
                    return parents.SelectMany(p => p.GetNamedChildren<INamedEntity>(this.Name));
                }
            } 

            //TODO: handle aliases

            //TODO: search better?

            var lex = ParentStatement.GetAncestorsAndSelf<NamedScope>().SelectMany(ns => ns.GetNamedChildren<INamedEntity>(this.Name));

            return lex;
        }

        public override IEnumerable<TypeDefinition> ResolveType()
        {
            return base.ResolveType();
        }
    }
}
