using System;
using System.IO;
using System.Collections.Generic;

public class MIDIReader
{
    public MIDIFile ReadFile(string path, string fileName)
    {
        string filePath = $"{path}/{fileName}.mid";
        MIDIFile newMIDI = new MIDIFile();

        using (FileStream fs = File.OpenRead(filePath))
        {
            if (!fs.CanRead)
                throw new Exception("Cannot Read File");

            //Read file header
            ReadHeader(fs, newMIDI);
            
            //We read every track in the file
            for(int i = 0; i < newMIDI.nTracks; i++)
            {
                ReadTrack(fs, newMIDI);
            }
        }

        return newMIDI;
    }

    static void ReadHeader(FileStream fs, MIDIFile newFile)
    {
        //Create the buffers
        byte[] frByte = new byte[4];
        byte[] twByte = new byte[2];

        //We read the Header, which starts with MThd
        //It's 4 bytes long
        fs.Read(frByte, 0, sizeof(int));

        //The fifth and the sixth bytes of the file are the Header length 
        //It's always 6, but we read it anyways
        fs.Read(frByte, 0, sizeof(int));

        //The next two bytes are the file format
        //Describes the way the tracks are organized
        fs.Read(twByte, 0, sizeof(short));
        newFile.format = (short)Reverse2Bytes(twByte);

        //Number of tracks
        fs.Read(twByte, 0, sizeof(short));
        newFile.nTracks = (short)Reverse2Bytes(twByte);

        //And the last two bytes are for the time division
        //Which has two formats: O for Pulses per Quarter Note
        //And 1 for SMPTE standard
        fs.Read(twByte, 0, sizeof(short));
        if((twByte[0] & 0x80) == 0)
        {
            newFile.PPQ = (short)Reverse2Bytes(twByte);
            newFile.tickDiv = true;
        }
        else
        {
            //Number of Frames per Second
            newFile.FramesPerSecond[0] = (byte)(twByte[0] & 0x7F);
            //Ticks per Frame
            newFile.FramesPerSecond[1] = twByte[1];

            newFile.tickDiv = false;
        }
    }

    void ReadTrack(FileStream fs, MIDIFile newFile)
    {
        //Read track identifier (MTrk)
        byte[] frByte = new byte[4];
        fs.Read(frByte, 0, sizeof(int));

        Track newTrack = new Track();

        //Read the track length
        fs.Read(frByte, 0, sizeof(int));
        newTrack.length = Reverse4Bytes(frByte);

        //Variable to mark the end of the track
        bool endOfTrack = false;

        //Keep track of the file ticks
        int currentTick = 0;

        byte lastStatus = 0;

        //We start reading the messages of the current track
        //Until we find the end of the track
        while(!endOfTrack)
        {
            //Before any message, we read the tick or the time delta
            byte[] nByte = new byte[1];
            currentTick += ReadValue(fs);

            //Then there is the status, or the message ID
            fs.Read(nByte, 0, sizeof(byte));
            byte status = nByte[0];

            //But if the leftmost bit of the status byte is not 1
            //Then means that this byte is data for another message
            //Which is the same as the previous message
            //e.g. B1 0B 7F 00 0B 7F
            //               ^
            //               |
            //The next byte is 0B or 11, however it does not correspond to any message
            //So you have to assign the previous status to the current
            //And go one position back in the FileStream
            if ((status & 0x80) == 0)
            {
                status = lastStatus;
                fs.Position = fs.Position - 1;
            }

            if ((status & 0x80) == 0x80)
            {
                lastStatus = status;

                if (status == 0xFF)
                {
                    fs.Read(nByte, 0, sizeof(byte));
                    byte eventType = nByte[0];

                    //Before we enter the method we check if it is the end of the track
                    if (eventType == (byte)MIDIEvents.EndOfTrack)
                    {
                        Event newEvent = new Event();
                        newEvent.eventType = MIDIEvents.EndOfTrack;
                        newEvent.tick = currentTick;

                        endOfTrack = true;
                        newTrack.events.Add(newEvent);
                    }

                    ReadMetaEvent(fs, newTrack, eventType, currentTick);
                }
                else if ((status & 0xF0) == 0xF0)
                {
                    ReadSysEvent(fs, newTrack, currentTick);
                }
                else
                {
                    ReadMIDIEvent(fs, newTrack, status, currentTick);
                }
            }
        }

        newFile.tracks.Add(newTrack);
    }

    static void ReadMIDIEvent(FileStream fs, Track nTrack, byte midiEvent, int currentTick)
    {
        //Create the MIDI Event and the buffer for the stream
        Event newEvent = new Event();
        byte[] nByte = new byte[1];

        if((midiEvent & 0xF0) == (byte)MIDIEvents.NoteOff)
        {
            newEvent.eventType = MIDIEvents.NoteOff;
            newEvent.tick = currentTick;
            newEvent.data = new byte[2];

            //Event Channel
            newEvent.channel = (byte)(midiEvent & 0x0F);

            //NoteID
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[0] = nByte[0];

            //Note Velocity
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[1] = nByte[0];

            nTrack.events.Add(newEvent);
            return;
        }
        else if ((midiEvent & 0xF0) == (byte)MIDIEvents.NoteOn)
        {
            newEvent.eventType = MIDIEvents.NoteOn;
            newEvent.tick = currentTick;
            newEvent.data = new byte[2];

            //Event Channel
            newEvent.channel = (byte)(midiEvent & 0x0F);

            //NoteID
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[0] = nByte[0];

            //Note Velocity
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[1] = nByte[0];

            nTrack.events.Add(newEvent);
            return;
        }
        else if ((midiEvent & 0xF0) == (byte)MIDIEvents.PolyphonicPressure)
        {
            newEvent.eventType = MIDIEvents.PolyphonicPressure;
            newEvent.tick = currentTick;
            newEvent.data = new byte[2];

            //Event Channel
            newEvent.channel = (byte)(midiEvent & 0x0F);

            //NodeID
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[0] = nByte[0];

            //Note Pressure
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[1] = nByte[0];

            nTrack.events.Add(newEvent);
            return;
        }
        else if ((midiEvent & 0xF0) == (byte)MIDIEvents.Controller)
        {
            newEvent.eventType = MIDIEvents.Controller;
            newEvent.tick = currentTick;
            newEvent.data = new byte[2];

            //Event Channel
            newEvent.channel = (byte)(midiEvent & 0x0F);

            //Controller Number
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[0] = nByte[0];

            //Controller Value
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[1] = nByte[0];

            nTrack.events.Add(newEvent);
            return;
        }
        else if ((midiEvent & 0xF0) == (byte)MIDIEvents.ProgramChange)
        {
            newEvent.eventType = MIDIEvents.ProgramChange;
            newEvent.tick = currentTick;
            newEvent.data = new byte[1];

            //Event Channel
            newEvent.channel = (byte)(midiEvent & 0x0F);

            //Specified Program
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[0] = nByte[0];

            nTrack.events.Add(newEvent);
            return;

        }
        else if ((midiEvent & 0xF0) == (byte)MIDIEvents.ChannelPressure)
        {
            newEvent.eventType = MIDIEvents.ChannelPressure;
            newEvent.tick = currentTick;
            newEvent.data = new byte[1];

            //Event Channel
            newEvent.channel = (byte)(midiEvent & 0x0F);

            //Pressure Amount
            fs.Read(nByte, 0, sizeof(byte));
            newEvent.data[0] = nByte[0];

            nTrack.events.Add(newEvent);
            return;
        }
        else if ((midiEvent & 0xF0) == (byte)MIDIEvents.PitchBend)
        {
            newEvent.eventType = MIDIEvents.PitchBend;
            newEvent.tick = currentTick;
            newEvent.data = new byte[2];

            //Event Channel
            newEvent.channel = (byte)(midiEvent & 0x0F);

            //Read 2 bytes and combine them into a 14 bit number
            int changePitch = 0;
            for(byte i = 0; i < 2; i++)
            {
                fs.Read(nByte, 0, sizeof(byte));
                byte tmp = nByte[0];
                changePitch = (changePitch << 7) | (tmp & 0x7F);
            }

            nTrack.events.Add(newEvent);
            return;
        }
    }

    static void ReadMetaEvent(FileStream fs, Track nTrack, byte eventType, int currentTick)
    {
        // The Meta messages are quite different from MIDI messages
        // Each meta message is constructed as follows:
        // 1st byte: Status (Always 0xFF) 2nd byte: Meta type
        // 3rd byte: Data length  N bytes: Data
        // We already have the status and type Meta
        // So the next thing to do is read the length of the message and the data

        //Create the Meta message
        Event newEvent = new Event();

        //Read message length
        byte[] nByte = new byte[1];
        fs.Read(nByte, 0, sizeof(byte));
        byte len = nByte[0];

        if (eventType > 0x07 || eventType == 0x20)
        {
            //First read the message data
            byte[] data = new byte[len];
            for (byte i = 0; i < len; i++)
            {
                fs.Read(nByte, 0, sizeof(byte));
                data[i] = nByte[0];
            }

            //Then we assign the 
            if (eventType == (byte)MIDIEvents.ChannelPrefix)
            {
                newEvent.eventType = MIDIEvents.ChannelPrefix;
                newEvent.data = new byte[len];
                newEvent.tick = currentTick;

                newEvent.data[0] = data[0];

                nTrack.events.Add(newEvent);
                return;
            }
            else if (eventType == (byte)MIDIEvents.Tempo)
            {
                //Create Event
                newEvent.eventType = MIDIEvents.Tempo;
                newEvent.data = new byte[len];
                newEvent.tick = currentTick;

                //Read The Next 3 Bytes To Obtain The Data
                for (byte i = 0; i < len; i++)
                {
                    newEvent.data[i] = data[i];
                }

                nTrack.events.Add(newEvent);
                return;
            }
            else if (eventType == (byte)MIDIEvents.SMPTEOffset)
            {
                newEvent.eventType = MIDIEvents.SMPTEOffset;
                newEvent.data = new byte[len];
                newEvent.tick = currentTick;

                for (byte i = 0; i < len; i++)
                {
                    newEvent.data[i] = data[i];
                }

                nTrack.events.Add(newEvent);
                return;
            }
            else if (eventType == (byte)MIDIEvents.TimeSignature)
            {
                newEvent.eventType = MIDIEvents.TimeSignature;
                newEvent.data = new byte[len];
                newEvent.tick = currentTick;

                for (byte i = 0; i < len; i++)
                {
                    newEvent.data[i] = data[i];
                }

                nTrack.events.Add(newEvent);
                return;
            }
            else if (eventType == (byte)MIDIEvents.KeySignature)
            {
                newEvent.eventType = MIDIEvents.KeySignature;
                newEvent.data = new byte[len];
                newEvent.tick = currentTick;

                for (byte i = 0; i < len; i++)
                {
                    newEvent.data[i] = data[i];
                }

                nTrack.events.Add(newEvent);
                return;
            }
            else if (eventType == (byte)MIDIEvents.SequencerSpf)
            {
                newEvent.eventType = MIDIEvents.SequencerSpf;
                newEvent.data = new byte[len];
                newEvent.tick = currentTick;

                for (byte i = 0; i < len; i++)
                {
                    newEvent.data[i] = data[i];
                }

                nTrack.events.Add(newEvent);
                return;
            }
        }

        //Given that the data of the message it's a string in ASCII for all the remain messages
        //We first read the string regardless of the Meta type and then we identify it
        string str = "";
        for (byte i = 0; i < len; i++)
        {
            //Read Each Character And Adds It To The String
            fs.Read(nByte, 0, sizeof(byte));
            str += Convert.ToChar(nByte[0]);
        }

        //Add the message to the corresponding list
        switch (eventType)
        {
            case (byte)MIDIEvents.Text:
                newEvent.eventType = MIDIEvents.Text;
                nTrack.text.Add($"{currentTick}: {str}");
                break;
            case (byte)MIDIEvents.Copyright:
                newEvent.eventType = MIDIEvents.Copyright;
                nTrack.copyright += str;
                break;
            case (byte)MIDIEvents.TrackName:
                newEvent.eventType = MIDIEvents.TrackName;
                nTrack.name = str;
                break;
            case (byte)MIDIEvents.InstrumentName:
                newEvent.eventType = MIDIEvents.InstrumentName;
                nTrack.instrumentName = str;
                break;
            case (byte)MIDIEvents.Lyrics:
                newEvent.eventType = MIDIEvents.Lyrics;
                nTrack.lyrics.Add($"{currentTick} - {str}");
                break;
            case (byte)MIDIEvents.Marker:
                newEvent.eventType = MIDIEvents.Marker;
                nTrack.lyrics.Add($"{currentTick} -- {str}");
                break;
            case (byte)MIDIEvents.CuePoint:
                newEvent.eventType = MIDIEvents.CuePoint;
                nTrack.text.Add($"{currentTick}: {str}");
                break;
        }

        newEvent.tick = currentTick;
        nTrack.events.Add(newEvent);
    }

    static void ReadSysEvent(FileStream fs, Track nTrack, int currentTick)
    {
        //Create Event
        Event newEvent = new Event();
        newEvent.eventType = MIDIEvents.other;

        //Read Event Length
        byte[] nByte = new byte[1];
        fs.Read(nByte, 0, sizeof(byte));
        byte len = nByte[0];

        //Read Event Data
        for(byte i = 0; i < len; i++)
        {
            fs.Read(nByte, 0, sizeof(byte));
        }

        newEvent.tick = currentTick;
        nTrack.events.Add(newEvent);
    }

    static int ReadValue(FileStream fs)
    {
        byte[] nByte = new byte[1];
        fs.Read(nByte, 0, sizeof(byte));

        //The time delta can continue another byte if the leftmost bit is 1
        byte value = nByte[0];
        if((value & 0x80) != 0)
        {
            //We grab the remaining 7 bits
            int fByte = 0;
            fByte &= 0x7F;

            //We only can push 4 bytes to a 32 bit integer
            //So we iterate three more times
            for(int i = 0; i < 3; i++)
            {
                fs.Read(nByte, 0, sizeof(byte));
                byte tmp = nByte[0];

                //We move the current bits to the left
                //And push another 7 bits
                fByte = (fByte << 7) | (tmp & 0x7F);

                if((tmp & 0x80) == 0)
                {
                    break;
                }
            }

            return fByte;
        }

        return value;
    }

    static int Reverse4Bytes(byte[] byteArray)
    {
        Array.Reverse(byteArray);
        int tmp = BitConverter.ToInt32(byteArray);
        Array.Clear(byteArray, 0, byteArray.Length);
        return tmp;
    }

    static ushort Reverse2Bytes(byte[] byteArray)
    {
        Array.Reverse(byteArray);
        ushort tmp = BitConverter.ToUInt16(byteArray);
        Array.Clear(byteArray, 0, byteArray.Length);
        return tmp;
    }

    public class MIDIFile
    {
        //Header data
        public short format;
        public short nTracks;
        //Time Division
        public bool tickDiv;
        public short PPQ;
        public byte[] FramesPerSecond = new byte[2];

        public List<Track> tracks = new List<Track>();
    }

    public class Track
    {
        public string copyright = "";
        public string name = "";
        public string instrumentName = "";
        public int length = 0;
        public List<Event> events = new List<Event>();
        public List<string> lyrics = new List<string>();
        public List<string> text = new List<string>();
    }

    public class Event
    {
        public MIDIEvents eventType;
        public int tick;
        public byte[] data;
        public int channel;
    }

    public enum MIDIEvents
    {
        //Midi messages
        NoteOff = 0x80,
        NoteOn = 0x90,
        PolyphonicPressure = 0xA0,
        Controller = 0xB0,
        ProgramChange = 0xC0,
        ChannelPressure = 0xD0,
        PitchBend = 0xE0,

        //Meta messages
        SequenceNumber = 0x00,
        Text = 0x01,
        Copyright = 0x02,
        TrackName = 0x03,
        InstrumentName = 0x04,
        Lyrics = 0x05,
        Marker = 0x06,
        CuePoint = 0x07,
        ChannelPrefix = 0x20,
        EndOfTrack = 0x2F,
        Tempo = 0x51,
        SMPTEOffset = 0x54,
        TimeSignature = 0x58,
        KeySignature = 0x59,
        SequencerSpf = 0x7F,

        //SystemExclusive
        other = 0xF
    }
}





















/*
Andamo' ruleta en una camioneta
Recogimo' la vaina, que llego la avioneta
Andamo' ruleta en una camioneta
Recogimo' la vaina, que llego la avioneta (Prr)
Coronao, coronao, coronao, now, now
Coronao, coronao, coronao, now, now (Ooh)
Coronao, coronao, coronao, now, now
Coronao, coronao, coronao, now, now
 */