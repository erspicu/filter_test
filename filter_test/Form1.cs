using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using XBRz_speed;
using hqx_speed;
using ScalexFilter;


using System.Diagnostics;

namespace xbrz_test
{
    public partial class Form1 : Form
    {

        Bitmap SourceBMP = new System.Drawing.Bitmap(Application.StartupPath + "\\load.png");

        int org_width, org_height;

        uint[] array_2X;//= new uint[256 * SourceBMP.Width];
        uint[] array_3X;//= new uint[384 * 192];
        uint[] array_4X;//= new uint[512 * 256];
        uint[] array_5X;//= new uint[SourceBMP.Height0 * 320];
        uint[] array_6X;//= new uint[768 * 384];
        uint[][] array_org;//= new uint[128][];

        public Form1()
        {
            InitializeComponent();

            HS_HQ.initTable();
            HS_XBRz.initTable();

            org_height = SourceBMP.Height;
            org_width = SourceBMP.Width;

            array_2X = new uint[org_width * org_height * 4];
            array_3X = new uint[org_width * org_height * 9];
            array_4X = new uint[org_width * org_height * 16];
            array_5X = new uint[org_width * org_height * 25];
            array_6X = new uint[org_width * org_height * 36];


            array_org = new uint[org_width][];

            for (int i = 0; i < org_width; i++)
                array_org[i] = new uint[org_height];

            for (int i = 0; i < SourceBMP.Width; i++)
                for (int j = 0; j < SourceBMP.Height; j++)
                    array_org[i][j] = (uint)((0x0 << 24) | (SourceBMP.GetPixel(i, j).R << 16) | (SourceBMP.GetPixel(i, j).G << 8) | SourceBMP.GetPixel(i, j).B);

            pictureBox1.Image = SourceBMP;



        }




        private void button1_Click(object sender, EventArgs e)
        {

            Stopwatch st = new Stopwatch();



            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                HS_XBRz.ScaleImage2X(array_org, array_2X, org_width, org_height);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp2x = new System.Drawing.Bitmap(org_width * 2, org_height * 2);

            for (int i = 0; i < org_width * 2; i++)
                for (int j = 0; j < org_height * 2; j++)
                    bmp2x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_2X[i + j * org_width * 2])));

            pictureBox2.Image = bmp2x;
        }


        private void button3_Click(object sender, EventArgs e)
        {

            Stopwatch st = new Stopwatch();



            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                HS_XBRz.ScaleImage3X(array_org, array_3X, org_width, org_height); ;
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp3x = new System.Drawing.Bitmap(org_width * 3, org_height * 3);

            for (int i = 0; i < org_width * 3; i++)
                for (int j = 0; j < org_height * 3; j++)
                    bmp3x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_3X[i + j * org_width * 3])));

            pictureBox3.Image = bmp3x;

        }

        private void button4_Click(object sender, EventArgs e)
        {

            Stopwatch st = new Stopwatch();



            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                HS_XBRz.ScaleImage4X(array_org, array_4X, org_width, org_height); ;
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp4x = new System.Drawing.Bitmap(org_width * 4, org_height * 4);

            for (int i = 0; i < org_width * 4; i++)
                for (int j = 0; j < org_height * 4; j++)
                    bmp4x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_4X[i + j * org_width * 4])));

            pictureBox4.Image = bmp4x;

        }

        private void button5_Click(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();



            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                HS_XBRz.ScaleImage5X(array_org, array_5X, org_width, org_height); ;
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp5x = new System.Drawing.Bitmap(org_width * 5, org_height * 5);

            for (int i = 0; i < org_width * 5; i++)
                for (int j = 0; j < org_height * 5; j++)
                    bmp5x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_5X[i + j * org_width * 5])));

            pictureBox5.Image = bmp5x;

        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();

            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                HS_XBRz.ScaleImage6X(array_org, array_6X, org_width, org_height); ;
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp6x = new System.Drawing.Bitmap(org_width * 6, org_height * 6);

            for (int i = 0; i < org_width * 6; i++)
                for (int j = 0; j < org_height * 6; j++)
                    bmp6x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_6X[i + j * org_width * 6])));

            pictureBox6.Image = bmp6x;

        }

        private void button6_Click(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();
            st.Restart();
            for (int i = 0; i < 2000; i++)
            {
                HS_HQ.Scale2(array_org, org_width, org_height, array_2X);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");

            Bitmap bmp2x = new System.Drawing.Bitmap(org_width * 2, org_height * 2);

            for (int i = 0; i < org_width * 2; i++)
                for (int j = 0; j < org_height * 2; j++)
                    bmp2x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_2X[i + j * org_width * 2])));

            pictureBox2.Image = bmp2x;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();



            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                HS_HQ.Scale3(array_org, org_width, org_height, array_3X);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp3x = new System.Drawing.Bitmap(org_width * 3, org_height * 3);

            for (int i = 0; i < org_width * 3; i++)
                for (int j = 0; j < org_height * 3; j++)
                    bmp3x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_3X[i + j * org_width * 3])));

            pictureBox3.Image = bmp3x;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();

            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                HS_HQ.Scale4(array_org, org_width, org_height, array_4X);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp4x = new System.Drawing.Bitmap(org_width * 4, org_height * 4);

            for (int i = 0; i < org_width * 4; i++)
                for (int j = 0; j < org_height * 4; j++)
                    bmp4x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_4X[i + j * org_width * 4])));

            pictureBox4.Image = bmp4x;
        }

        private void button9_Click(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();
            st.Restart();
            for (int i = 0; i < 2000; i++)
            {
                ScalexTool.toScale2x_dx(array_org, org_width, org_height, array_2X);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");

            Bitmap bmp2x = new System.Drawing.Bitmap(org_width * 2, org_height * 2);

            for (int i = 0; i < org_width * 2; i++)
                for (int j = 0; j < org_height * 2; j++)
                    bmp2x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_2X[i + j * org_width * 2])));

            pictureBox2.Image = bmp2x;
        }

        private void button10_Click(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();

            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                ScalexTool.toScale3x_dx(array_org, org_width, org_height, array_3X);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp3x = new System.Drawing.Bitmap(org_width * 3, org_height * 3);

            for (int i = 0; i < org_width * 3; i++)
                for (int j = 0; j < org_height * 3; j++)
                    bmp3x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_3X[i + j * org_width * 3])));

            pictureBox3.Image = bmp3x;
        }

        private void button11_Click(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();

            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                ScalexTool.toScale6x_dx(array_org, org_width, org_height, array_6X);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp6x = new System.Drawing.Bitmap(org_width * 6, org_height * 6);

            for (int i = 0; i < org_width * 6; i++)
                for (int j = 0; j < org_height * 6; j++)
                    bmp6x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_6X[i + j * org_width * 6])));

            pictureBox6.Image = bmp6x;
        }

        private void button12_Click(object sender, EventArgs e)
        {

            Stopwatch st = new Stopwatch();



            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                ScalexTool.toScale4x_dx(array_org, org_width, org_height, array_4X);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp4x = new System.Drawing.Bitmap(org_width * 4, org_height * 4);

            for (int i = 0; i < org_width * 4; i++)
                for (int j = 0; j < org_height * 4; j++)
                    bmp4x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_4X[i + j * org_width * 4])));

            pictureBox4.Image = bmp4x;
        }

        private void button13_Click(object sender, EventArgs e)
        {
            Stopwatch st = new Stopwatch();

            st.Restart();
            for (int i = 0; i < 2000; i++)
            {

                HS_HQ.Scale6(array_org, org_width, org_height, array_6X);
            }
            st.Stop();

            Console.WriteLine("all time : " + st.ElapsedMilliseconds);
            int fps = (int)((double)2000 / ((double)st.ElapsedMilliseconds / (double)1000));
            Console.WriteLine("fps : " + fps + "\r\n");


            Bitmap bmp6x = new System.Drawing.Bitmap(org_width * 6, org_height * 6);

            for (int i = 0; i < org_width * 6; i++)
                for (int j = 0; j < org_height * 6; j++)
                    bmp6x.SetPixel(i, j, Color.FromArgb((int)(0xff000000 | array_6X[i + j * org_width * 6])));

            pictureBox6.Image = bmp6x;
        }
    }

}
