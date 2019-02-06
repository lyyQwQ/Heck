﻿using Chroma.Beatmap.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chroma.Misc {

    public static class NoteScaling {

        public delegate void HandleNoteScalingDelegate(ref NoteData note, ref float scale);
        public static event HandleNoteScalingDelegate HandleNoteScalingEvent;

        public static float GetNoteScale(NoteData note) {
            ChromaLogger.Log("GetNoteScale called!");
            try {
                float s = ChromaNoteScaleEvent.GetScale(note.time);
                HandleNoteScalingEvent?.Invoke(ref note, ref s);
                ChromaLogger.Log("Note init : HNSE null? " + (HandleNoteScalingEvent == null).ToString());
                return s;
            } catch (Exception e) {
                ChromaLogger.Log(e);
                return 1;
            }
        }

    }

}
