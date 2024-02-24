# Basic MIDI Reader for Unity
Hey this is a basic script that I have worked, while I was trying to learn MIDI.

This script can read the following messages:
 - MIDI events
   - Note Off
   - Note On
   - Polyphonic Pressure
   - Controller
   - Program Change
   - Channel Pressure
   - Pitch Bend
 - SysEx events
 - Meta events
   - Sequence Number
   - Text
   - Copyright
   - Track Name
   - Instrument Name
   - Lyrics
   - Marker
   - Cue Point
   - Program Name
   - Device name
   - MIDI Channel Prefix
   - MIDI Port
   - End of Track
   - Tempo
   - SMPTE Offset
   - Time Signature
   - Key Signature
   - Sequencer Specific Event

## Script Structure

The class that will contain all the data will be MIDIFile, every time you want to read a MIDI file this class will be returned. 

The variables format and nTracks are from the MIDI Header. For the time format you have PPQ (Which stands for "Pulses per Quarter Note") and FramesPerSecond, you can find either of them in a MIDI file. And the list will store all the tracks that can be read from the file.

```
public class MIDIFile
{
  public short format;
  public short nTracks;

  public bool tickDiv;
  public short PPQ;
  public byte[] FramesPerSecond = new byte[2];

  public List<Track> tracks = new List<Track>();
}
``` 

Now each track will be assigned a name, an instrument name, a copyright advice, and a length. Finally, there will be three lists, storing the events occurring on the track.

```
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
```

Lastly, each event or message will have:
- An event identifier
- The tick on which it occurs
- An array of bytes containing all the information of the message
- A channel to which the message applies.

```
public class Event
{
  public MIDIEvents eventType;
  public int tick;
  public byte[] data;
  public int channel;
}
```

---

Every message will be in the 'events' list of the 'tracks' list, inside the MIDIFile class. Except for the following messages:
 - Meta Events
   - Text (Text List)
   - Lyrics (Lyrics List)
   - Marker (Lyrics List)
   - Cue Point (Text List)

## Resources
If you are interested in learning more about MIDI, its messages, structure or want to make your own implementation, in the Resources section I will leave several web pages and videos that helped me.
But, as always, it is best to consult the official MIDI specification.
I tried to leave comments on the script explaining how it works, but I highly recommend you to see and read the resources I found.