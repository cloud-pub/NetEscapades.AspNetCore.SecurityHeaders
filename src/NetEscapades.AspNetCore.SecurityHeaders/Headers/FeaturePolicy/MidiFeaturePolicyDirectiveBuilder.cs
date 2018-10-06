﻿namespace NetEscapades.AspNetCore.SecurityHeaders.Infrastructure
{
    /// <summary>
    /// Controls whether the current document is allowed to use the Web MIDI API.
    /// If disabled in a document, the promise returned by <code>requestMIDIAccess()</code>
    /// must reject with a DOMException parameter.
    /// </summary>
    public class MidiFeaturePolicyDirectiveBuilder : FeaturePolicyDirectiveBuilder
    {
        public MidiFeaturePolicyDirectiveBuilder() : base("midi")
        {
        }
    }
}