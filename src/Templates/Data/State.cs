namespace Templates.Data {
    /// <summary>
    /// States of Sytax parser
    /// </summary>
    public enum State {
        Undefined,
        DefinitionBegin,
        DefinitionEnd,
        DefinitionNameBegin,
        DefinitionNameEnd,
        DefinitionNestingBegin,
        DefinitionNestingEnd,
        DefinitionParameterStart,
        DefinitionParameterEnd,
        DefinitionModelTypeStart,
        DefinitionModelTypeEnd,
        OutputStart,
        OutputEnd,
        UsageBegin,
        UsageEnd,
        ModelParameterBegin,
        ModelParameterEnd,
        ParameterBegin,
        ParameterEnd,
        SyntaxError,
        CompileError,
        OtherError
    }
}