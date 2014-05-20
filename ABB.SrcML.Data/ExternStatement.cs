﻿/******************************************************************************
 * Copyright (c) 2014 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *    Patrick Francis (ABB Group) - initial API, implementation, & documentation
 *    Vinay Augustine (ABB Group) - initial API, implementation, & documentation
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ABB.SrcML.Data {
    /// <summary>
    /// Represents an extern statement in C/C++ that specifies a linkage type.
    /// Note that this is expected to be something like <code>extern "C" { #include&lt;stdio.h&gt; }</code>.
    /// 
    /// Declarations that use extern as a storage specifier, such as <code>extern int myGlobalVar;</code>, will not be parsed as ExternStatements.
    /// </summary>
    public class ExternStatement : Statement {
        public string LinkageType { get; set; }
    }
}