using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.IO;
using System.Runtime.InteropServices;
using Emgu.CV.Util;

/*
2018_04_08_ZhaoXu
相机标定的目的： 获取摄像机的内参和外参矩阵（同时也会得到每一幅标定图像的选择和平移矩阵
                 内参和外参系数可以对之后相机拍摄的图像就进行矫正，得到畸变相对很小的图像

相机标定的输入： 标定图像上所有内角点的图像坐标，
                 标定板图像上所有内角点的空间三维坐标（一般情况下假定图像位于Z=0平面上

相机标定的输出： 摄像机的内参、外参系数。 
  
 */

namespace EmguCalibrateCamera
{
	public partial class Form1 : Form
	{
		public Form1()
		{

			InitializeComponent();

		}
		private void Form1_Load(object sender, EventArgs e)
		{

		}
		//Emgu中不可以直接利用at对矩阵数据的像素进行操作，自己写个转换方法
		public static double[] GetDoubleArray( Mat mat)
		{
			double[] temp = new double[mat.Height * mat.Width];
			Marshal.Copy(mat.DataPointer, temp, 0, mat.Height * mat.Width);
			return temp;
		}
		public static void SetArray( Mat mat, double[] data)
		{
			Marshal.Copy(data, 0, mat.DataPointer, mat.Height * mat.Width);
		}
		//相机标定
		private void CalibraCamera(object sender, EventArgs e)
		{
			//图像标定
			StreamReader sin = new StreamReader("calibdata.txt");
			//读取每一副图像，从中提出角点，然后对角点进行亚像素精确化
			Console.WriteLine("开始提取角点");
			int image_count = 0;//图像数量
			Size image_size = new Size();//图像尺寸
			int width = 4;
			int height = 6;
			Size board_size = new Size(4, 6);//标定版上每行每列的角点数目
			int CornerNum = board_size.Width * board_size.Height;//每张图片上的角点总数
			int nImage = 14;
			VectorOfPointF Npointsl = new VectorOfPointF();

			string filename;
			int count = 0;//用于存储角点个数
			Console.WriteLine("count = " + count);
			MCvPoint3D32f[][] object_points = new MCvPoint3D32f[nImage][];//保存标定板上角点的三维坐标
			PointF[][] corner_count = new PointF[nImage][];
			while ((filename = sin.ReadLine()) != null)
			{
				image_count++;
				//用于观察检验输出
				Console.WriteLine("image_count = " + image_count);
				//输出检验
				//打开获取到的图像
				Image<Bgr, byte> imageInput = new Image<Bgr, byte>(new Bitmap(Image.FromFile(filename)));
				pictureBox1.Image = imageInput.ToBitmap();
				if (image_count == 1)//读入第一张图片时获取图像宽高信息
				{
					Console.WriteLine("<---成功读取第一张图片--->");
					image_size.Width = imageInput.Cols;
					image_size.Height = imageInput.Rows;
					Console.WriteLine("image_size.Width  = " + image_size.Width);
					Console.WriteLine("image_size.Hright = " + image_size.Height);
				}
				//提取角点
				Mat view_gray = new Mat();
				CvInvoke.CvtColor(imageInput, view_gray, ColorConversion.Rgb2Gray);
				//提取内角点（内角点与标定板的边缘不接触）
				//对每一张标定图片，提取角点信息
				/*
				第一个参数Image，传入拍摄的棋盘图Mat图像，必须是8位的灰度或者彩色图像；
                第二个参数patternSize，每个棋盘图上内角点的行列数，一般情况下，行列数不要相同，便于后续标定程序识别标定板的方向；
                第三个参数corners，用于存储检测到的内角点图像坐标位置，一般用元素是VectorOfPoint类型表示
                第四个参数flage：用于定义棋盘图上内角点查找的不同处理方式，有默认值。 
				*/
				CvInvoke.FindChessboardCorners(view_gray, board_size, Npointsl, CalibCbType.AdaptiveThresh);
				corner_count[image_count - 1] = new PointF[24];	
				for (int i = 0; i < 24; i++)
				{
					corner_count[image_count - 1][i] = Npointsl.ToArray()[i];
				}
				//为了提高标定精度，需要在初步提取的角点信息上进一步提取亚像素信息，降低相机标定偏差
				//亚像素精确化FindCornerSubPix()
				/*
                第一个参数corners，初始的角点坐标向量，同时作为亚像素坐标位置的输出，所以需要是浮点型数据，一般用元素是PointF[][]向量来表示
                第二个参数winSize，大小为搜索窗口的一半；
                第三个参数zeroZone，死区的一半尺寸，死区为不对搜索区的中央位置做求和运算的区域。它是用来避免自相关矩阵出现某些可能的奇异性。当值为（-1，-1）时表示没有死区；
                第四个参数criteria，定义求角点的迭代过程的终止条件，可以为迭代次数和角点精度两者的组合；
				*/
				view_gray.ToImage<Gray, byte>().FindCornerSubPix(corner_count, new Size(11, 11), new Size(-1, -1), new MCvTermCriteria(30, 0.1));
				//在图像上显示角点位置,在图片中标记角点
				Console.WriteLine("第" + image_count + "个图片已经被标记角点");
				//DrawChessboardCorners用于绘制被成功标定的角点
				/*
				第一个参数image，8位灰度或者彩色图像；
                第二个参数patternSize，每张标定棋盘上内角点的行列数；
                第三个参数corners，初始的角点坐标向量，同时作为亚像素坐标位置的输出，所以需要是浮点型数据
                第四个参数patternWasFound，标志位，用来指示定义的棋盘内角点是否被完整的探测到，true表示别完整的探测到，函数会用直线依次连接所有的内角点，作为一个整体，false表示有未被探测到的内角点，这时候函数会以（红色）圆圈标记处检测到的内角点； 
				*/
				CvInvoke.DrawChessboardCorners(view_gray, board_size, Npointsl, true);//非必需，仅用做测试
				pictureBox2.Image = view_gray.ToImage<Bgr, byte>().ToBitmap();
				count = image_count;
				CvInvoke.WaitKey(500);//暂停0.5秒*/
			}
			Console.WriteLine("角点提取完成！！！");
			//摄像机标定
			Console.WriteLine("开始标定");
			//摄像机内参数矩阵
			Mat cameraMatrix = new Mat(3, 3, DepthType.Cv32F, 1);
			//畸变矩阵
			//摄像机的5个畸变系数：k1,k2,p1,p2,k3
			Mat distCoeffs = new Mat(1, 5, DepthType.Cv32F, 1);
			//旋转矩阵R
			Mat[] rotateMat = new Mat[nImage];
			for(int i=0;i<nImage;i++)
			{
				rotateMat[i] = new Mat(3, 3, DepthType.Cv32F, 1);
			}
			//平移矩阵T
			Mat[] transMat = new  Mat[nImage];
			for (int i = 0; i < nImage; i++)
			{
				transMat[i] = new Mat(3, 1, DepthType.Cv32F, 1);
			}
			//初始化标定板上角点的三维坐标
			List<MCvPoint3D32f> object_list = new List<MCvPoint3D32f>();
			for (int k = 0; k < nImage; k++)
			{
				object_list.Clear();
				for (int i = 0; i < height; i++)
				{
					for (int j = 0; j < width; j++)
					{
						object_list.Add(new MCvPoint3D32f(j , i , 0f));
					}
				}
				object_points[k] = object_list.ToArray();
			}
			//相机标定
			//获取到棋盘标定图的内角点图像坐标之后，使用CalibrateCamera函数进行相机标定，计算相机内参和外参矩阵
			/*
			第一个参数objectPoints，为世界坐标系中的三维点。在使用时，应该输入一个三维坐标点的向量的向量MCvPoint3D32f[][]，即需要依据棋盘上单个黑白矩阵的大小，计算出（初始化）每一个内角点的世界坐标。
            第二个参数imagePoints，为每一个内角点对应的图像坐标点。和objectPoints一样，应该输入PointF[][]类型变量；
            第三个参数imageSize，为图像的像素尺寸大小，在计算相机的内参和畸变矩阵时需要使用到该参数；
            第四个参数cameraMatrix为相机的内参矩阵。输入一个Mat cameraMatrix即可，如Mat cameraMatrix=Mat(3,3,CV_32FC1,Scalar::all(0));
            第五个参数distCoeffs为畸变矩阵。输入一个Mat distCoeffs=Mat(1,5,CV_32FC1,Scalar::all(0))即可 
			第六个参数CalibType相机标定类型
			第七个参数criteria是最优迭代终止条件设定
			第八个参数out Mat[]类型的旋转矩阵
			第九个参数out Mat[]类型的平移矩阵
			*/
			//在使用该函数进行标定运算之前，需要对棋盘上每一个内角点的空间坐标系的位置坐标进行初始化
			//标定的结果是生成相机的内参矩阵cameraMatrix、相机的5个畸变系数distCoeffs
			//另外每张图像都会生成属于自己的平移向量和旋转向量
			CvInvoke.CalibrateCamera(object_points, corner_count, image_size, cameraMatrix,
				  distCoeffs, CalibType.RationalModel, new MCvTermCriteria(30,0.1),  out rotateMat, out transMat);
			Console.WriteLine("标定完成");							 
			/*标定评价略*/
			//利用标定结果对图像进行畸变校正
			//mapx和mapy为输出的x/y坐标重映射参数
			/*
			Mat mapx = new Mat(image_size, DepthType.Cv32F, 1);
			Mat mapy = new Mat(image_size, DepthType.Cv32F, 1);
			//可选输入，是第一和第二相机坐标之间的旋转矩阵
			Mat R = new Mat(3, 3, DepthType.Cv32F, 1);
			//输出校正之后的3x3摄像机矩阵
			Mat newCameraMatrix = new Mat(3, 3, DepthType.Cv32F, 1);
			*/
			Console.WriteLine("保存矫正图像");

			StreamReader sin_test = new StreamReader("calibdata1.txt");
			string filename_test;
			for (int i = 0; i < nImage; i++)
			{
				//InitUndistortRectifyMap用来计算畸变映射
				//CvInvoke.InitUndistortRectifyMap(cameraMatrix, distCoeffs, R, newCameraMatrix, image_size, DepthType.Cv32F, mapx, mapy);
				if((filename_test = sin_test.ReadLine()) != null)
				{
					Image<Bgr, byte> imageSource = new Image<Bgr, byte>(new Bitmap(Image.FromFile(filename_test)));
					Image<Bgr, byte> newimage = imageSource.Clone();
					CvInvoke.Undistort(imageSource,newimage,cameraMatrix,distCoeffs);
					//Remap把求得的映射应用到图像上
					//CvInvoke.Remap(imageSource, newimage, mapx, mapy, Inter.Linear, BorderType.Constant, new MCvScalar(0));
					pictureBox3.Image = imageSource.ToBitmap();								
					pictureBox4.Image = newimage.ToBitmap();					
					CvInvoke.WaitKey(500);
				}
			}
			Console.WriteLine("标定结束！");
		}
		private void button1_Click_1(object sender, EventArgs e)
		{
			CalibraCamera(sender,e);
		}
	}
}
