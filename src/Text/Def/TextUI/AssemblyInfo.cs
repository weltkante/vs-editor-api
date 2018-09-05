//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Versioning;
using System.Security.Permissions;


//
// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
//

[assembly: ComponentGuarantees(ComponentGuaranteesOptions.Stable)]


[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]
#pragma warning disable 618
[assembly: SecurityPermission (SecurityAction.RequestMinimum, Flags = SecurityPermissionFlag.Execution)]
#pragma warning restore 618
[assembly: ReliabilityContract(Consistency.MayCorruptProcess, Cer.MayFail)]

[assembly: InternalsVisibleTo("Microsoft.VisualStudio.Text.Implementation, PublicKey=0024000004800000940000000602000000240000525341310004000001000100e57febc1f220077550a65e338d3d15d7cbd189cf4f62f7c3829dcb2f8441a6c40631d172e3deb4dc0bb7237b44ec9daeb9bd7d72c3d64c4f52b968795443cb58bc341583c29440345b8c35f72f6a31aecb2903376136f8fc35779bb422eb643f8668fa6605c697bff927e3bb10745328ff878bd1b7e42bbcb839f04baa8460bd")]
