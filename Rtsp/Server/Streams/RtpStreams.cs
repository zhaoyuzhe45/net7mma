﻿/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://net7mma.codeplex.com/
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. http://www.asti-trans.com)

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Media.Rtsp.Server.Streams
{
    /// <summary>
    /// Provides the basic operations for consuming a remote rtp stream for which there is an existing <see cref="SessionDescription"/>
    /// </summary>
    public class RtpSource : SourceStream, Media.Common.IThreadReference
    {
        public RtpSource(string name, Uri source) : base(name, source) { }
        
        public bool RtcpDisabled { get { return m_DisableQOS; } set { m_DisableQOS = value; } }

        public virtual Rtp.RtpClient RtpClient { get; protected set; }

        //This will take effect after the change, existing clients will still have their connection
        public bool ForceTCP { get { return m_ForceTCP; } set { m_ForceTCP = value; } } 
        
        //System.Drawing.Image m_lastDecodedFrame;
        //internal virtual void DecodeFrame(Rtp.RtpClient sender, Rtp.RtpFrame frame)
        //{
        //    if (RtpClient == null || RtpClient != sender) return;
        //    try
        //    {
        //        //Get the MediaDescription (by ssrc so dynamic payload types don't conflict
        //        Media.Sdp.MediaDescription mediaDescription = RtpClient.GetContextBySourceId(frame.SynchronizationSourceIdentifier).MediaDescription;
        //        if (mediaDescription.MediaType == Sdp.MediaType.audio)
        //        {
        //            //Could have generic byte[] handlers OnAudioData OnVideoData OnEtc
        //            //throw new NotImplementedException();
        //        }
        //        else if (mediaDescription.MediaType == Sdp.MediaType.video)
        //        {
        //            if (mediaDescription.MediaFormat == 26)
        //            {
        //                OnFrameDecoded(m_lastDecodedFrame = (new RFC2435Stream.RFC2435Frame(frame)).ToImage());
        //            }
        //            else if (mediaDescription.MediaFormat >= 96 && mediaDescription.MediaFormat < 128)
        //            {
        //                //Dynamic..
        //                //throw new NotImplementedException();
        //            }
        //            else
        //            {
        //                //0 - 95 || >= 128
        //                //throw new NotImplementedException();
        //            }
        //        }
        //    }
        //    catch
        //    {
        //        return;
        //    }
        //}

        public override void Start()
        {
            //Add handler for frame events
            if (State == StreamState.Stopped)
            {
                if (RtpClient != null)
                {
                    RtpClient.Connect();

                    base.Ready = true;

                    base.Start();
                }
            }
        }

        public override void Stop()
        {
            //Remove handler
            if (State == StreamState.Started)
            {
                if (RtpClient != null) RtpClient.Disconnect();

                base.Ready = false;

                base.Stop();
            }
        }

        public override void Dispose()
        {
            if (Disposed) return;
            base.Dispose();
            if (RtpClient != null) RtpClient.Dispose();
        }

        public RtpSource(string name, Sdp.SessionDescription sessionDescription)
            : base(name, new Uri(Rtp.RtpClient.RtpProtcolScheme + "://" + ((Sdp.Lines.SessionConnectionLine)sessionDescription.ConnectionLine).IPAddress))
        {
            if (sessionDescription == null) throw new ArgumentNullException("sessionDescription");

            RtpClient = Media.Rtp.RtpClient.FromSessionDescription(SessionDescription = sessionDescription);
        }

        IEnumerable<System.Threading.Thread> Common.IThreadReference.ReferencedThreads
        {
            get { return RtpClient != null ? Utility.Yield(RtpClient.m_WorkerThread) : null; }
        }
    }

    /// <summary>
    /// Provides the basic opertions for any locally created Rtp data
    /// </summary>
    public class RtpSink : RtpSource, IMediaSink
    {
        public RtpSink(string name, Uri source) : base(name, source) { }

        public virtual bool Loop { get; set; }

        protected Queue<Media.Common.IPacket> Packets = new Queue<Media.Common.IPacket>();

        //public double MaxSendRate { get; protected set; }

        //Fix

        public void SendData(byte[] data)
        {
            if (RtpClient != null) RtpClient.OnRtpPacketReceieved(new Rtp.RtpPacket(data, 0));
        }

        public void EnqueData(byte[] data)
        {
            if (RtpClient != null) Packets.Enqueue(new Rtp.RtpPacket(data, 0));
        }

        //

        public void SendPacket(Media.Common.IPacket packet)
        {
            if (RtpClient != null)
            {
                if (packet is Rtp.RtpPacket) RtpClient.OnRtpPacketReceieved(packet as Rtp.RtpPacket);
                else if (packet is Rtcp.RtcpPacket) RtpClient.OnRtcpPacketReceieved(packet as Rtcp.RtcpPacket);
            }
        }

        public void EnquePacket(Media.Common.IPacket packet)
        {
            if (RtpClient != null) Packets.Enqueue(packet);
        }

        public void SendReports()
        {
            if (RtpClient != null) RtpClient.SendReports();
        }

        internal virtual void SendPackets()
        {
            while (State == StreamState.Started)
            {
                try
                {
                    if (Packets.Count == 0)
                    {
                        System.Threading.Thread.Sleep(0);
                        continue;
                    }

                    //Dequeue a frame or die
                     Media.Common.IPacket packet = Packets.Dequeue();

                     SendPacket(packet);

                    //If we are to loop images then add it back at the end
                    if (Loop) Packets.Enqueue(packet);

                    //Check for bandwidth and sleep if necessary
                }
                catch (Exception ex)
                {
                    if (ex is System.Threading.ThreadAbortException) return;
                    continue;
                }
            }
        }
    }
}
