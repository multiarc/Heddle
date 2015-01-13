namespace Templates.Core.Data {
    /// <summary>
    /// States of Sytax parser
    /// </summary>
    public enum State {
        /// <summary>
        /// Automat State is undefined (Sequence not begun)
        /// </summary>
        Undefined,

        /// <summary>
        /// Sequence started, parsing inside content
        /// </summary>
        SequenceBegin,

        /// <summary>
        /// Sequence ended (Syntax parser should return false)
        /// </summary>
        SequenceEnd,

        /// <summary>
        /// Template name sequence started
        /// </summary>
        NameBegin,

        /// <summary>
        /// Template name sequence ended
        /// </summary>
        NameEnd,

        /// <summary>
        /// Data Name string started
        /// </summary>
        DataNameBegin,

        /// <summary>
        /// Data Name string ended
        /// </summary>
        DataNameEnd,

        /// <summary>
        /// Additional Data Name string started
        /// </summary>
        AdditionalDataNameBegin,

        /// <summary>
        /// Additional Data Name string ended
        /// </summary>
        AdditionalDataNameEnd,

        /// <summary>
        /// Full parameter string started
        /// </summary>
        ParameterBegin,

        /// <summary>
        /// Full parameter string ended
        /// </summary>
        ParameterEnd
    }
}