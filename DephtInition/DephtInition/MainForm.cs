﻿//   Copyright 2013 Giancarlo Todone

//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at

//       http://www.apache.org/licenses/LICENSE-2.0

//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

//   for info: http://www.stareat.it/sp.aspx?g=3ce7bc36fb334b8d85e6900b0bdf11c3

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace DepthInition
{
    public partial class MainForm : Form
    {
        BitmapShowForm _showContrForm;
        BitmapShowForm _showRGBForm;
        BitmapShowForm _showdepthForm;
        ValuesGraphForm _graphForm;

        FloatMap _maxMap = null; // TODO: introduce middle step "raw map"

        // TODO: databind these:
        public float StackInterDistance { get; set; }
        public float SpikeFilterTreshold { get; set; }
        public int MultiResSteps { get; set; }
        public float CurveReliabilityTreshold { get; set; } // TODO: make this dependant on parable's "aspect ratio"
        public int PreShrinkTimes { get; set; }
        public int CapHolesFilterEmisize { get; set; }
        public float CloserPictureDistance { get; set; }
        public float BlurSigma { get; set; }
        public int BlurTimes { get; set; }
        public int ShrinkContrastTimes { get; set; }

        public MainForm()
        {
            InitializeComponent();
            _showContrForm = new BitmapShowForm() { Text = "Contrast" };
            _showContrForm.Show();

            _showRGBForm = new BitmapShowForm() { Text = "RGB" };
            _showRGBForm.Show();

            _showdepthForm = new BitmapShowForm() { Text = "Depth (approx)" };
            _showdepthForm.Show();

            _graphForm = new ValuesGraphForm() { Text = "graph"};
            _graphForm.Show();

            _showRGBForm.pnlDisplayBitmap.MouseDown += new MouseEventHandler(pnlDisplayBitmap_MouseDown);
            _showContrForm.pnlDisplayBitmap.MouseDown += new MouseEventHandler(pnlDisplayBitmap_MouseDown);

            _showdepthForm.pnlDisplayBitmap.MouseDown += new MouseEventHandler(checkSpikes);

            btnGo.Tag = false;

            SpikeFilterTreshold = 1.8f;
            MultiResSteps = 3;
            CurveReliabilityTreshold = 0.2f;
            PreShrinkTimes = 1;
            CapHolesFilterEmisize = 10;
            StackInterDistance = 8;
            CloserPictureDistance = 100;
            BlurSigma = 1.8f;
            BlurTimes = 3;
            ShrinkContrastTimes = 5;
        }

        void checkSpikes(object sender, MouseEventArgs e)
        {
            var typedSender = sender as Control;

            int w = _imgfs[0].W;
            int h = _imgfs[0].H;

            float xProp = (float)w / (float)typedSender.Width;
            float yProp = (float)h / (float)typedSender.Height;

            int x = (int)(e.X * xProp);
            int y = (int)(e.Y * yProp);

            Console.WriteLine("[{0},{1}] -> [{2},{3}]", e.X, e.Y, x, y);

            Console.WriteLine("spike: {0}",MapUtils.GetSpikeHeight(_maxMap, x, y));
        }

        void pnlDisplayBitmap_MouseDown(object sender, MouseEventArgs e)
        {
            var typedSender = sender as Control;

            int w = _imgfs[0].W;
            int h = _imgfs[0].H;
            
            float xProp = (float)w / (float)typedSender.Width;
            float yProp = (float)h / (float)typedSender.Height;

            int x = (int)(e.X * xProp);
            int y = (int)(e.Y * yProp);

            Console.WriteLine("[{0},{1}] -> [{2},{3}]", e.X, e.Y, x, y);


            float[] vs = new float[_imgfs.Count];

            int i = 0;
            foreach (var im in _imgfs)
            {
                vs[i] = im[x, y];
                Console.WriteLine("{1} {0:0.0000}", im[x, y], i++);
            }


            _graphForm.Values = vs;
        }

        string[] _fileNames = null;

        int _displayedBmpIdx = -1;

        public int DisplayedBmpIdx
        {
            get 
            {
                if ((_displayedBmpIdx < 0) && (_fileNames.Length > 0))
                {
                    _displayedBmpIdx = 0;
                }
                
                return _displayedBmpIdx;
            }

            set
            {
                if ((_fileNames == null) || (_fileNames.Length <= 0))
                {
                    return;
                }

                if (value >= _fileNames.Length)
                {
                    _displayedBmpIdx = _fileNames.Length -1 ;
                }
                else
                {
                    if (value < 0)
                    {
                        _displayedBmpIdx = (_fileNames.Length > 0) ? 0 : -1;
                    }
                    else
                    {
                        _displayedBmpIdx = value;
                    }
                }
                                
                _showRGBForm.DisplayedBitmap = new Bitmap(_fileNames[_displayedBmpIdx]);
                this.Text = string.Format("displaying image {0}/{1}", _displayedBmpIdx, _fileNames.Length - 1);

                float max = MapUtils.GetMapMax(_imgfs[_displayedBmpIdx]);
                _showContrForm.DisplayedBitmap = MapUtils.Map2Bmp(_imgfs[_displayedBmpIdx], 255.0f / max);
            }
        }

        List<FloatMap> _imgfs = new List<FloatMap>();

        int _w = -1;
        int _h = -1;

        private void btnGo_Click(object sender, EventArgs e)
        {
            if ((bool)(btnGo.Tag) == false)
            {
                btnGo.Text = "cancel";
                btnGo.Tag = true;
                backgroundWorker1.RunWorkerAsync();
            }
            else
            {
                btnGo.Text = "go";
                btnGo.Tag = false;
                backgroundWorker1.CancelAsync();
            }
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case '+':
                    ++DisplayedBmpIdx;
                    break;
                case '-':
                    --DisplayedBmpIdx;
                    break;
            }
        }

        string pickName(string name, string ext)
        {
            int i = 0;
            string result;
            while (File.Exists(result = string.Format("{0}{1}.{2}", name, i, ext)))
            {
                ++i;
            }
            return result;
        }

        float getMaxIdx(int x, int y)
        {
            int l = _imgfs.Count;
            float[] vals = new float[l];
            for (int i = 0; i < l; ++i )
            {
                vals[i] = _imgfs[i][x, y];
            }

            return getDist(vals);
        }

        FloatMap getMaxMap()
        {
            int h = _imgfs[0].H;
            int w = _imgfs[0].W;

            FloatMap imgfOut = new FloatMap(w, h);

            for (int y =0; y<h; ++y)
            {
                for (int x = 0; x < w; ++x )
                {
                    float v = getMaxIdx(x, y);
                    imgfOut[x, y] = v;// < 0 ? -1 : 255 - v * 255 / _imgfs.Count; // MOVED into map2BmpDepth
                }            
            }

            return imgfOut;
        }

        void smoothDepth()
        {
            int h = _imgfs[0].H;
            int w = _imgfs[0].W;
            int stride = _imgfs[0].Stride;
            
            int l = _imgfs.Count;

            for (int imgIdx = 1; imgIdx < l-1; ++imgIdx)
            {
                int lineStart = 0;
                for (int y = 0; y < h; ++y)
                {
                    int i = lineStart;
                    for (int x = 0; x < w; ++x)
                    {
                        _imgfs[imgIdx][i] = _imgfs[imgIdx][i] * 0.5f + (_imgfs[imgIdx + 1][i] + _imgfs[imgIdx - 1][i]) * 0.25f;
                        ++i;
                    }
                    lineStart += stride;
                }
            }


        }

        float getDist(float[] values)
        {
            // this is basically trying to fit a parable
            // to the points which focus rank is higher than average
            // THIS IS A CHEAP TRICK soon to be substituted
            // with RANSAC or RANSAC-like technique...

            try
            {
                float min = 1000000;
                int minPos = -1;

                float max = 0;
                int maxPos = -1;

                int l = values.Length;
                float v = 0;
                float accu = 0;

                for (int i = 0; i < l; ++i)
                {
                    v = values[i];
                    accu += v;

                    if (v < min)
                    {
                        min = v;
                        minPos = i;
                    }

                    if (v > max)
                    {
                        max = v;
                        maxPos = i;
                    }
                }

                float average = accu / l;

                List<PointF> ps = new List<PointF>(l);

                for (int i = 0; i < l; ++i)
                {
                    v = values[i];
                    if (v > average)
                    {
                        ps.Add(new PointF(i, values[i]));
                    }
                }

                var fittedParamsTemp = CurveFunctions.FindPolynomialLeastSquaresFit(ps, 2);

                if (fittedParamsTemp[2] > 0)
                {
                    return -1;
                }

                var errorTemp = Math.Sqrt(CurveFunctions.ErrorSquared(ps, fittedParamsTemp));

                //if (errorTemp > 0.5f)
                //{
                //    return -1;
                //}

                if (max - average < CurveReliabilityTreshold)
                {
                    return -1;
                }

                // -b / 2a 
                float res = (float)(-fittedParamsTemp[1] / (2 * fittedParamsTemp[2]));
                return (res >= l) || (res <= 0) ? -1 : res;
            }
            catch { return -1; }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _w = -1;
            _h = -1;
            _fileNames = openFileDialog1.FileNames;
        }

        private void rGBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _showRGBForm.Show();
            _showContrForm.Focus();
        }

        private void contrastToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _showContrForm.Show();
            _showContrForm.Focus();
        }

        private void pointDepthGraphToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _graphForm.Show();
            _graphForm.Focus();
        }

        private void depthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _showdepthForm.Show();
            _showdepthForm.Focus();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            if ((_fileNames == null) || (_fileNames.Length <= 3))
            {
                return;
            }

            _imgfs.Clear();
            
            int fileCount = _fileNames.Length;

            float progressStep = 100.0f / (fileCount * 2.0f);
            float progress = 0;

            backgroundWorker1.ReportProgress((int)progress,"converting");

            // for each selected file 
            for (int fileIdx = 0; fileIdx < fileCount; ++fileIdx)
            {
                string fileName = _fileNames[fileIdx];

                // load bitmap
                using (var _bmp = new Bitmap(fileName))
                {
                    if (fileIdx == 0)
                    {
                        _h = _bmp.Height;
                        _w = _bmp.Width;
                    }
                    else
                    {

                        if ((_h != _bmp.Height) || (_w != _bmp.Width))
                        {
                            MessageBox.Show("Images must have same size!");
                            return;
                        }
                    }

                    FloatMap imgf;

                    // get luminance map
                    imgf = MapUtils.HalfMap(MapUtils.Bmp2Map(_bmp), PreShrinkTimes);
                    
                    _imgfs.Add(imgf);
                }

                // update and report progress
                progress += progressStep;
                backgroundWorker1.ReportProgress((int)progress);

                // check for cancellation
                if (backgroundWorker1.CancellationPending)
                {
                    return;
                }
            }

            List<FloatMap> newImgfs = new List<FloatMap>();

            backgroundWorker1.ReportProgress((int)progress, "getting contrast");

            // for each luminance map
            foreach (var imgf in _imgfs)
            {
                // get contrast, then shrink result (averaging pixels)
                FloatMap newImgf = MapUtils.HalfMap(MapUtils.GetMultiResContrastEvaluation(imgf, MultiResSteps), ShrinkContrastTimes);

                newImgfs.Add(newImgf);

                // update and report progress
                progress += progressStep;
                backgroundWorker1.ReportProgress((int)progress);

                // check for cancellation
                if (backgroundWorker1.CancellationPending)
                {
                    return;
                }
            }

            _imgfs = newImgfs;

            smoothDepth(); smoothDepth();

            _maxMap = getMaxMap();

            // smooth
            for (int i = 0; i < BlurTimes; ++i)
            {
                _maxMap = MapUtils.GaussianBlur(_maxMap, BlurSigma);
            }

            // filter out spikes
            _maxMap = MapUtils.SpikesFilter(_maxMap, SpikeFilterTreshold);

            // cap holes
            _maxMap = MapUtils.CapHoles(_maxMap, CapHolesFilterEmisize);
            
            // TODO: correct the bell-distorsion

            savePLY();

            saveObj();
        }

        private void savePLY()
        {
            int rw = _maxMap.W;
            int rh = _maxMap.H;

            int count = 0;

            for (int y = 0; y < rh; ++y)
            {
                for (int x = 0; x < rw; ++x)
                {
                    if (_maxMap[x, y] > 0)
                    {
                        ++count;
                    }
                }
            }

            var xk = _w / rw;
            var yk = _h / rh;

            // load last bitmap
            using (var bmp = new Bitmap(_fileNames[_fileNames.Length - 1]))
            {
                BitmapData dstData = bmp.LockBits(new Rectangle(0, 0, _w, _h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                int pixelSize = 4;

                // open ply file
                using (var sw = new StreamWriter(pickName("shot", "ply")))
                {
                    // write ply header
                    sw.WriteLine("ply");
                    sw.WriteLine("format ascii 1.0");
                    sw.WriteLine("comment PLY generated with DepthInition by Giancarlo Todone");
                    sw.WriteLine("element vertex " + count);
                    sw.WriteLine("property float x");
                    sw.WriteLine("property float y");
                    sw.WriteLine("property float z");
                    sw.WriteLine("property uchar red");
                    sw.WriteLine("property uchar green");
                    sw.WriteLine("property uchar blue");
                    sw.WriteLine("end_header");

                    float invScale = 1.0f / (float)Math.Max(rw, rh);
                    float xOffs = -0.5f * (float)rw * invScale;
                    float yOffs = -0.5f * (float)rh * invScale;
                    float zk = StackInterDistance;
                    float zOffs = StackInterDistance * (float)(_imgfs.Count) * 0.5f;

                    unsafe
                    {
                        // access bitmap data
                        int dstStride = dstData.Stride;
                        for (int y = 0; y < rh; ++y)
                        {
                            int by = y * yk;
                            byte* dstRow = (byte*)dstData.Scan0 + dstStride * by;
                            for (int x = 0; x < rw; ++x)
                            {
                                var v = _maxMap[x, y];
                                if (v >= 0)
                                {
                                    int i = x * xk * pixelSize;
                                    byte b = dstRow[i];
                                    byte g = dstRow[i + 1];
                                    byte r = dstRow[i + 2];

                                    float px, py, pz;
                                    getPerspectiveCorrected3DPoint(x, y, out  px, out  py, out  pz);

                                    // write point
                                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:0.000} {1:0.000} {2:0.000} {3} {4} {5}", px, py, pz, r, g, b));
                                }
                            }
                            dstRow += dstStride;
                        }
                    }
                }

                bmp.UnlockBits(dstData);
            }
        }

        private void saveObj()
        {
            int rw = _maxMap.W;
            int rh = _maxMap.H;

            using (var sw = new StreamWriter(pickName("model", "obj")))
            {
                // write obj header
                sw.WriteLine("# Obj generated with DepthInition by Giancarlo Todone");

                // write vertexes
                for (int y = 1; y < rh - 1; ++y)
                {
                    for (int x = 1; x < rw - 1; ++x)
                    {
                        float px, py, pz;
                        getPerspectiveCorrected3DPoint(x, y, out px, out py, out pz);

                        // write point
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "v {0:0.000} {1:0.000} {2:0.000} 1.0", px, py, pz));
                    }
                }

                // write texture coordinates
                for (int y = 1; y < rh - 1; ++y)
                {
                    for (int x = 1; x < rw - 1; ++x)
                    {
                        var v = _maxMap[x, y];
                        float px = (float)x / (float)rw;
                        float py = (float)y / (float)rh;

                        // write tex coord
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "vt {0:0.000} {1:0.000} 0", px, py));
                    }
                }

                // write normals
                for (int y = 1; y < rh - 1; ++y)
                {
                    for (int x = 1; x < rw - 1; ++x)
                    {
                        float p1x, p1y, p1z, p2x, p2y, p2z, p3x, p3y, p3z, p4x, p4y, p4z,
                            v3x, v3y, v3z;
                        getPerspectiveCorrected3DPoint(x - 1, y, out p1x, out p1y, out p1z);
                        getPerspectiveCorrected3DPoint(x + 1, y, out p2x, out p2y, out p2z);
                        getPerspectiveCorrected3DPoint(x, y - 1, out p3x, out p3y, out p3z);
                        getPerspectiveCorrected3DPoint(x, y + 1, out p4x, out p4y, out p4z);

                        cross(p2x - p1x, p2y - p1y, p2z - p1z, p4x - p3x, p4y - p3y, p4z - p3z, out v3x, out v3y, out v3z);
                        norm(ref v3x, ref v3y, ref v3z);

                        // write normal
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "vn {0:0.000} {1:0.000} {2:0.000} 1.0", v3x, v3y, v3z));
                    }
                }

                // write faces
                int rw2 = rw - 2;
                for (int y = 0; y < rh - 3; ++y)
                {
                    for (int x = 0; x < rw - 3; ++x)
                    {
                        int i1 = x + y * rw2 + 1;
                        int i2 = i1 + 1;
                        int i3 = i2 + rw2;
                        int i4 = i1 + rw2;
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8} ", i1, i1, i1, i2, i2, i2, i3, i3, i3));
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8} ", i1, i1, i1, i3, i3, i3, i4, i4, i4));
                    }
                }
            }
        }

        void getPerspectiveCorrected3DPoint(int x, int y, out float px, out float py, out float pz)
        {
            // TODO: cache all these
            int rw = _maxMap.W;
            int rh = _maxMap.H;

            var xk = _w / rw;
            var yk = _h / rh;

            float invScale = 1.0f / (float)Math.Max(rw, rh);
            float xOffs = -0.5f * (float)rw * invScale;
            float yOffs = -0.5f * (float)rh * invScale;
            float zk = StackInterDistance;
            float zOffs = StackInterDistance * (float)(_imgfs.Count) * 0.5f;



            var v = _maxMap[x, y];
            pz = v * zk;

            float perspCorr = (CloserPictureDistance + pz) / CloserPictureDistance; // TODO: consider FOV for xy/z aspect

            px = (((float)x * invScale + xOffs) * perspCorr * 200.0f); // TODO: fix once an for all conversions between virtual units and real world ones
            py = -(((float)y * invScale + yOffs) * perspCorr * 200.0f);
            pz = zOffs - pz; 
        }

        void norm(ref float x, ref float y, ref float z)
        {
            float d = (float)(1.0 / Math.Sqrt(x * x + y * y + z * z));
            x = x * d;
            y = y * d;
            z = z * d;
        }

        void cross(float ax, float ay, float az, float bx, float by, float bz, out float cx, out float cy, out float cz)
        {
            cx = ay * bz - az * by;
            cy = az * bx - ax * bz;
            cz = ax * by - ay * bx;
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            gaugeProgressBar1.Value = e.ProgressPercentage;
            string s = e.UserState as string;
            if (s != null)
            {
                gaugeProgressBar1.Label = s;    
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnGo.Enabled = true;
            btnGo.Text = "go";
            btnGo.Tag = false;
            
            if (gaugeProgressBar1.Value == 0)
            {
                if ((_fileNames == null) || (_fileNames.Length <= 3))
                {
                    MessageBox.Show("Nothing to do (select more files)");
                    return;
                }
                return;
            }

            gaugeProgressBar1.Value = 0;

            if (!e.Cancelled)
            {
                try
                {
                    DisplayedBmpIdx = 0;
                    _showdepthForm.DisplayedBitmap = MapUtils.Map2BmpDepthMap(_maxMap, 1, _imgfs.Count);
                }
                catch { }
                gaugeProgressBar1.Label = "done";
            }
            else
            {
                gaugeProgressBar1.Label = "canceled";
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            updShrinkTimes.DataBindings.Add("Value", this, "PreShrinkTimes");

            updCloserPictureDistance.DataBindings.Add("Value", this, "CloserPictureDistance");
            updStackInterDistance.DataBindings.Add("Value", this, "StackInterDistance");
            updMultiResSteps.DataBindings.Add("Value", this, "MultiResSteps");
            updCurveReliabilityTreshold.DataBindings.Add("Value", this, "CurveReliabilityTreshold");

            updSpikeFilterTreshold.DataBindings.Add("Value", this, "SpikeFilterTreshold");

            updCapHolesSize.DataBindings.Add("Value", this, "CapHolesFilterEmisize");

            updBlurSigma.DataBindings.Add("Value", this, "BlurSigma");
            updBlurTimes.DataBindings.Add("Value", this, "BlurTimes");

            updShrinkContrastTimes.DataBindings.Add("Value", this, "ShrinkContrastTimes");
        }
    }
}
