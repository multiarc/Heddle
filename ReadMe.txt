Copyright (c) 2012-2013 Aliaksandr Kukrash
Text Templating Language
v2.0

Performance Oriented:
i7-2600, DDR3 - 1833 (8-9-8-24) -- Up to 150 Mb/s text generation with C# code, HTML encoding, multiple repeaters, partial templates on the random data

General Description

1) Template is a text document wich represents source for engine.
2) Data for template is an object, where "properties" is data fields for engine
3) Template Extension is a part of engine, can be placed in any Assembly and should marked with attribute Templating.Attributes.NameAttribute, Templating.Attributes.TypeAttribute and Templating.Attributes.AdditionalTypeAttribute
	external assemblies should be parsed by Templates.Core.TemplateFactory.LoadAddTemplatesFromAssembly(). Any template extension class should implement ITemplate, 
	also you can inherit from Templating.AbstractTemplate abstract class to have partially implementented rules, or implement/override it by yourself.

System Template

\ -- <%
/ -- %>
{ -- [
} -- ]
<%<model>{Library.Namespace.Type}%>//Model Type Specifier for static templates
<%<import>{Library.Namespace}%>//Make Types visible in specified namespace

Helper Extensions:
1)expression can only modify existing variables or create local named
2)call replaces data and return something other

<%<renderType:call:expression>ObjectMethodContainer/*callvirt*/ ObjectParameresContainer 
{ Argument1 = Argument2 + 3;/*Do whatever you want, except method calls*/ } 
{ MethodName(Argument1, Argument2) } 
{ Render Template } 
%>
Example:

<%<list:method:method>DataForMehthod /*Data passes into "data" parameter of the following method right below*/
{
	/*Person is Templating.Types.Person for example, need to import namespace first or use full type name*/
	IQueriable<Person>(data)/*data -- name of formal parameter for method, you can set whatever you want here*/
	{
		return data.MethodName(data.Argument1, data.Argument2);
		/*Returns IQueriable for example(!)*/
	}
}
{
	IEnumerable<Person>(inputData)
	{
		return inputData.AsIEnumerable();
		/*Here it takes object returned by MethodName() as itself, so you can access methods, properties inside it*/ 
	}
}
{ 
	<tr>
		<td>
			<%Name%>
		</td>
	</tr>
}
%>

Data passed from each extension to other from up to down. First extension got data specified in whole template (DataForMehthod) if not specified then model got under current level of enclose.
DataForMethod is property of model under current level of enclose of whole list template

Also any method can be renderer as itself:

<%<method:method:method>ObjectMethodContainer ObjectParametersContainer 
{
	/*Person is Templating.Types.Person for example, need to import namespace first or use full type name*/
	IQueriable<Person>(data) /*Returns IQueriable<Person>*/
	{
		return MethodName(data.Argument1, data.Argument2);
	}
}
{
	IEnumerable<Person>(inputData)
	{
		return inputData.AsIEnumerable();
		/*Here it takes object returned by MethodName() as itself, so you can access methods, properties inside it*/ 
	}
}
{ 
	FastString(data)
	{
		FastStringBuilder result = new FastStringBuilder();
		foreach(var item in data)
		{
			result.Append(@"
			<tr>
				<td>
					"+item.Name+@"
				</td>
			</tr>");
		}
		return result.ToFastString();
	}
}
%>

These two records almost the same, except you write own code to render data.