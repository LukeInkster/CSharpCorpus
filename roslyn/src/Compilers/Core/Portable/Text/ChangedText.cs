﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace Microsoft.CodeAnalysis.Text
{
    internal sealed class ChangedText : SourceText
    {
        private readonly SourceText _newText;

        // store old text weakly so we don't form unwanted chains of old texts (especially chains of ChangedTexts)
        // It is only used to identify the old text in GetChangeRanges which only returns the changes if old text matches identity.
        private readonly WeakReference<SourceText> _weakOldText;
        private readonly ImmutableArray<TextChangeRange> _changes;

        public ChangedText(SourceText oldText, SourceText newText, ImmutableArray<TextChangeRange> changeRanges)
            : base(checksumAlgorithm: oldText.ChecksumAlgorithm)
        {
            Debug.Assert(newText != null);
            Debug.Assert(newText is CompositeText || newText is SubText || newText is StringText || newText is LargeText);
            Debug.Assert(oldText != null);
            Debug.Assert(oldText != newText);
            Debug.Assert(!changeRanges.IsDefault);

            _newText = newText;
            _weakOldText = new WeakReference<SourceText>(oldText);
            _changes = changeRanges;
        }

        public override Encoding Encoding
        {
            get { return _newText.Encoding; }
        }

        public IEnumerable<TextChangeRange> Changes
        {
            get { return _changes; }
        }

        public override int Length
        {
            get { return _newText.Length; }
        }

        internal override int StorageSize
        {
            get { return _newText.StorageSize; }
        }

        internal override ImmutableArray<SourceText> Segments
        {
            get { return _newText.Segments; }
        }

        internal override SourceText StorageKey
        {
            get { return _newText.StorageKey; }
        }

        public override char this[int position]
        {
            get { return _newText[position]; }
        }

        public override string ToString(TextSpan span)
        {
            return _newText.ToString(span);
        }

        public override SourceText GetSubText(TextSpan span)
        {
            return _newText.GetSubText(span);
        }

        public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            _newText.CopyTo(sourceIndex, destination, destinationIndex, count);
        }

        public override SourceText WithChanges(IEnumerable<TextChange> changes)
        {
            // compute changes against newText to avoid capturing strong references to this ChangedText instance.
            // _newText will only ever be one of CompositeText, SubText, StringText or LargeText, so calling WithChanges on it 
            // will either produce a ChangeText instance or the original instance in case of a empty change.
            var changed = _newText.WithChanges(changes) as ChangedText;  
            if (changed != null)
            {
                return new ChangedText(this, changed._newText, changed._changes);
            }
            else 
            {
                // change was empty, so just return this same instance
                return this;
            }
        }

        public override IReadOnlyList<TextChangeRange> GetChangeRanges(SourceText oldText)
        {
            if (oldText == null)
            {
                throw new ArgumentNullException(nameof(oldText));
            }

            if (this == oldText)
            {
                return TextChangeRange.NoChanges;
            }

            SourceText actualOldText;
            if (_weakOldText.TryGetTarget(out actualOldText))
            {
                if (actualOldText == oldText)
                {
                    // same identity, so the changes must be the ones we have.
                    return _changes;
                }

                if (actualOldText.GetChangeRanges(oldText).Count == 0)
                {
                    // the bases are different instances, but the contents are considered to be the same.
                    return _changes;
                }
            }

            return ImmutableArray.Create(new TextChangeRange(new TextSpan(0, oldText.Length), _newText.Length));
        }

        /// <summary>
        /// Computes line starts faster given already computed line starts from text before the change.
        /// </summary>
        protected override TextLineCollection GetLinesCore()
        {
            SourceText oldText;
            TextLineCollection oldLineInfo;

            if (!_weakOldText.TryGetTarget(out oldText) || !oldText.TryGetLines(out oldLineInfo))
            {
                // no old line starts? do it the hard way.
                return base.GetLinesCore();
            }

            // compute line starts given changes and line starts already computed from previous text
            var lineStarts = ArrayBuilder<int>.GetInstance();
            lineStarts.Add(0);

            // position in the original document
            var position = 0;

            // delta generated by already processed changes (position in the new document = position + delta)
            var delta = 0;

            // true if last segment ends with CR and we need to check for CR+LF code below assumes that both CR and LF are also line breaks alone
            var endsWithCR = false;

            foreach (var change in _changes)
            {
                // include existing line starts that occur before this change
                if (change.Span.Start > position)
                {
                    if (endsWithCR && _newText[position + delta] == '\n')
                    {
                        // remove last added line start (it was due to previous CR)
                        // a new line start including the LF will be added next
                        lineStarts.RemoveLast();
                    }

                    var lps = oldLineInfo.GetLinePositionSpan(TextSpan.FromBounds(position, change.Span.Start));
                    for (int i = lps.Start.Line + 1; i <= lps.End.Line; i++)
                    {
                        lineStarts.Add(oldLineInfo[i].Start + delta);
                    }

                    endsWithCR = oldText[change.Span.Start - 1] == '\r';

                    // in case change is inserted between CR+LF we treat CR as line break alone, 
                    // but this line break might be retracted and replaced with new one in case LF is inserted  
                    if (endsWithCR && change.Span.Start < oldText.Length && oldText[change.Span.Start] == '\n')
                    {
                        lineStarts.Add(change.Span.Start + delta);
                    }
                }

                // include line starts that occur within newly inserted text
                if (change.NewLength > 0)
                {
                    var changeStart = change.Span.Start + delta;
                    var text = GetSubText(new TextSpan(changeStart, change.NewLength));

                    if (endsWithCR && text[0] == '\n')
                    {
                        // remove last added line start (it was due to previous CR)
                        // a new line start including the LF will be added next
                        lineStarts.RemoveLast();
                    }

                    // Skip first line (it is always at offset 0 and corresponds to the previous line)
                    for (int i = 1; i < text.Lines.Count; i++)
                    {
                        lineStarts.Add(changeStart + text.Lines[i].Start);
                    }

                    endsWithCR = text[change.NewLength - 1] == '\r';
                }

                position = change.Span.End;
                delta += (change.NewLength - change.Span.Length);
            }

            // include existing line starts that occur after all changes
            if (position < oldText.Length)
            {
                if (endsWithCR && _newText[position + delta] == '\n')
                {
                    // remove last added line start (it was due to previous CR)
                    // a new line start including the LF will be added next
                    lineStarts.RemoveLast();
                }

                var lps = oldLineInfo.GetLinePositionSpan(TextSpan.FromBounds(position, oldText.Length));
                for (int i = lps.Start.Line + 1; i <= lps.End.Line; i++)
                {
                    lineStarts.Add(oldLineInfo[i].Start + delta);
                }
            }

            return new LineInfo(this, lineStarts.ToArrayAndFree());
        }
    }
}