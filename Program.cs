using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace MURROR{

    //  순서 1. 이미지 입력 받기 -> 자동으로 같은 폴더내의 같은 이름의 GT.json 찾아 가져오기()
    //  순서 2. 측정 입력 받기 -> 서버에서 PD 받아와서 필요한 17개의 포인트만 받기
    //  순서 3. OKS 계산 -> 각 클래스별(키포인트별)로 OKS 계산
    //  순서 4. TP, FP, TN, FN 판단 -> Precision 및 Recall 계산
    //  순서 5. AP 계산(각 클래스별 AP계산)
    //  순서 6. mAP 계산(모든 클래스에 대한 mAP)


    //  각 키포인트 클래스
    public enum KEYNAME{
        Nose = 0, LeftEye = 1, RightEye = 2, LeftEar = 3, RightEar = 4, LeftShoulder =5, RightShoulder = 6,
        LeftElbow = 7, RightElbow = 8, LeftWrist = 9, RightWrist = 10, LeftHip = 11, RightHip = 12,
        LeftKnee = 13, RightKnee = 14, LeftAnkle = 15, RightAnkle = 16
    }

    //  각 키포인트별 판단
    public enum CONFUSION_MATRIX { TP, TN, FP, FN }

    // GT&PD를 담는 구조체
    public struct KeyPoint2D {
        public float score;
        public float x;
        public float y; 
    }
    
    // AP 계산하기 위한 구조체
    public struct ComputeData {
        public string filename;
        public KEYNAME keyname;
        public KeyPoint2D gt;
        public KeyPoint2D pd;
        public double OKSi;
        public CONFUSION_MATRIX[] cMatrix;
        public float dd;
        public float ss;
        
        public int[] tp;
        public int[] fp;

        public float[] precision;
        public float[] recall;

    }

    // AP를 계산하기 위한 키포인트별 TP, FN 총합계 (0.5, 0.75, 0.9, 0.95의 배열)
    public struct APComputeData{
        public KEYNAME keyname;
        public int[] tpall;
        public int[] fnall;
    }


 
    public class Program{  
           //  순서대로 실행
           //  이미지 모두 입력 후에 모든 이미지의 경로를 받는 걸로 바꾸면 됨        
        static public void Main(){
            mAPCalculate mAPc = new mAPCalculate();
            List<string> gtlist = new List<string>();
            List<string> pdlist = new List<string>();

            string gtImagePath = @"C:\Users\swlee\Desktop\210817\Develop\Data\COCOmAP\gt";
            string pdImagePath = @"C:\Users\swlee\Desktop\210817\Develop\Data\COCOmAP\pd";

            DirectoryInfo di = new DirectoryInfo(gtImagePath);
            foreach(FileInfo File in di.GetFiles()){
                string gtfile = File.FullName;
                gtlist.Add(mAPc.ImageInputGT(gtfile));
            }

            DirectoryInfo di2 = new DirectoryInfo(pdImagePath);
            foreach(FileInfo File in di2.GetFiles()){
                string pdfile = File.FullName;
                pdlist.Add(mAPc.ImageInputPD(pdfile));
            }
            
            mAPc.CalculateOKS(gtlist, pdlist);
        }
    }

    class mAPCalculate{

        // 상수 값 정의
        public static readonly float[] FallOffs = {
            0.026f,         //  nose    0
            0.025f, 0.025f, //  eyes    1,2
            0.035f, 0.035f, //  ears    3,4
            0.079f, 0.079f, //  shoulders   5,6
            0.072f, 0.072f, //  elbows  7,8
            0.062f, 0.062f, //  wrists  9,10
            0.107f, 0.107f, //  hips    11,12
            0.087f, 0.087f, //  knees   13,14
            0.089f, 0.089f, //  ankles  15,16
        };


        //  순서 1. 이미지 입력 받기 -> 자동으로 같은 폴더내의 같은 이름의 GT.json 찾아 가져와 데이터 넣기
        public string ImageInputGT(string ImagePath){
                string[] files = {"",};
                string Path = ImagePath.Substring(0, ImagePath.LastIndexOf('\\'));
                string ImageName = ImagePath.Substring(Path.Length+1, 12);
                try{
                    files = Directory.GetFiles(Path, ImageName+".json", SearchOption.AllDirectories);
                }catch(IOException ex){
                    Console.WriteLine(ex);
                }
                return files[0];
            }

        //  순서 2. 측정 입력 받기 -> 서버에서 PD 받아와서 필요한 17개의 포인트만 받아와 데이터 넣기(임시적으로 파일로 대체)
        public string ImageInputPD(string ImagePath){
            string[] files = {"",};
            string Path = ImagePath.Substring(0, ImagePath.LastIndexOf('\\'));
            string ImageName = ImagePath.Substring(Path.Length+1, 12);
            try{
                files = Directory.GetFiles(Path, ImageName+".json", SearchOption.AllDirectories);
            }catch(IOException ex){
                Console.WriteLine(ex);
            }
            return files[0];
        }

        //  순서 3. OKS 계산
        public void CalculateOKS(List<string> gt, List<string> pd){
            //filename별 Nose&&Eye데이터를 담는 리스트
            List<List<ComputeData>> listlistcd = new List<List<ComputeData>>();                
            
            // 3-1. gt, pd 데이터 저장
            // gt 데이터를 구조체에 저장
            foreach(string gtdata in gt){
                List<ComputeData> listcd = new List<ComputeData>();
                string data = File.ReadAllText(gtdata);
                JObject json = JObject.Parse(data);
                string Path = gtdata.Substring(0, gtdata.LastIndexOf('\\'));
                string ImageName = gtdata.Substring(Path.Length+1, 12);
                foreach(JToken at in json["predictions"]){
                    ComputeData cd = new ComputeData();
                    JProperty jp = at.ToObject<JProperty>();
                    string key = jp.Name;
                    KEYNAME kn = (KEYNAME)Enum.Parse(typeof(KEYNAME), key);
                    cd.filename = ImageName;                        
                    cd.keyname = kn;
                    float x = (float)json["predictions"][key]["x"];
                    float y = (float)json["predictions"][key]["y"];   
                    float score = (float)json["predictions"][key]["score"];                     
                    cd.gt.x = x;
                    cd.gt.y = y;
                    cd.gt.score = score;
                    cd.cMatrix = new CONFUSION_MATRIX[4];
                    cd.dd = 0.0f;
                    listcd.Add(cd);
                }
                listlistcd.Add(listcd);
            }
            
            // pd는 gt를 담았던 구조체에 같이 담아야함
            // pd는 서버에서 받아오면 수정되지 않은 결과값이 25개의 키포인트가 존재, 17개의 선정의된 KEYNAME만 담기 위해 비교 후 기존 구조체를 수정
            foreach(string pddata in pd){
                string data = File.ReadAllText(pddata);
                JObject json = JObject.Parse(data);
                string Path = pddata.Substring(0, pddata.LastIndexOf('\\'));
                string ImageName = pddata.Substring(Path.Length+1, 12);
                foreach(JToken at in json["predictions"]){
                    JProperty jp = at.ToObject<JProperty>();
                    string key = jp.Name;
                    if(Enum.IsDefined(typeof(KEYNAME), key)){
                        KEYNAME kn = (KEYNAME)Enum.Parse(typeof(KEYNAME), key);
                        float x = (float)json["predictions"][key]["x"];
                        float y = (float)json["predictions"][key]["y"];   
                        float score = (float)json["predictions"][key]["score"];  
                        for(int i = 0; i<listlistcd.Count; i++){
                            for(int j = 0; j <listlistcd[i].Count; j++){
                                if(listlistcd[i][j].keyname == kn && listlistcd[i][j].filename == ImageName){
                                    ComputeData cd = listlistcd[i][j];
                                    cd.pd.x = x;
                                    cd.pd.y = y;
                                    cd.pd.score = score;
                                    listlistcd[i][j] = cd;
                                }
                            }                            
                        }
                    }                        
                }                    
            }

            //  3-2. OKS 계산 및 TP, FP, TN, FN 판단
            for(int i = 0; i<listlistcd.Count; i++){
                //각 파일별 바운딩 박스가 초기화
                float[] bMin = {1080f, 1920f};
                float[] bMax = {0f, 0f};

                // 3-2-1. pd-gt의 제곱
                for(int j = 0; j<listlistcd[i].Count; j++){
                    float dx = listlistcd[i][j].gt.x - listlistcd[i][j].pd.x;
                    float dy = listlistcd[i][j].gt.y - listlistcd[i][j].pd.y;
                    float dd = (float)dx * dx + dy * dy;
                    ComputeData cd = listlistcd[i][j];
                    cd.dd = dd;
                    listlistcd[i][j] = cd;
                }                    

                //  3-2-2. 바운딩 박스 계산(x, y의 MAX와 MIN)
                for(int j = 0; j<17; j++) {
                if(listlistcd[i][j].gt.x < bMin[0]) bMin[0] = listlistcd[i][j].gt.x;
                if(listlistcd[i][j].gt.x > bMax[0]) bMax[0] = listlistcd[i][j].gt.x;
                if(listlistcd[i][j].gt.y < bMin[1]) bMin[1] = listlistcd[i][j].gt.y;
                if(listlistcd[i][j].gt.y > bMax[1]) bMax[1] = listlistcd[i][j].gt.y;
                }

                //  3-2-3. 바운딩 박스 면적 계산 후 구조체 수정
                for(int j =0; j<17; j++){
                    float ss = (bMax[0]-bMin[0])*(bMax[1]-bMin[1]);
                    ComputeData cd = listlistcd[i][j];
                    cd.ss = ss;
                    listlistcd[i][j] = cd;
                }

                //  3-2-4. OKS 계산
                //  구조체의 GT가 0이거나 PD가 0이면 OKS는 0이어야한다.
                for(int j = 0; j<17; j++){                        
                    ComputeData re_cd = listlistcd[i][j];
                    double oks = 0.0;
                    if((listlistcd[i][j].gt.x == 0 && listlistcd[i][j].gt.y == 0)||(listlistcd[i][j].pd.x == 0 && listlistcd[i][j].pd.y == 0)){}
                    else oks = Math.Exp(-re_cd.dd/(2*(re_cd.ss*(FallOffs[j]*FallOffs[j]))));
                    re_cd.OKSi = oks;
                    listlistcd[i][j] = re_cd;
                }
                
                //  3-2-5. TP, FP, TN, FN 판단
                //  TP : 기준 OKS보다 높을 경우
                //  TN : GT가 0일때 PD도 0일 경우(OKS가 0이기 때문에 따로 구현)
                //  FP : GT는 0이지만 PD는 0이 아닐 경우
                //  FN : GT는 이 아니지만 PD는 0을 가르킬 경우
                float[] oksflag = {0.5f, 0.75f, 0.9f, 0.95f};
                int index = 0;
                foreach(float f in oksflag){
                    CONFUSION_MATRIX Confusion_Matrix;
                    for(int j = 0; j<listlistcd[i].Count; j++){
                    if(f<=listlistcd[i][j].OKSi){
                        Confusion_Matrix = (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "TP");
                    }else if(listlistcd[i][j].gt.x == 0 && listlistcd[i][j].gt.y == 0 && listlistcd[i][j].pd.x == 0 && listlistcd[i][j].pd.y == 0){
                        Confusion_Matrix = (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "TN");
                    }else if((listlistcd[i][j].gt.x == 0 && listlistcd[i][j].pd.x > 0) || (listlistcd[i][j].gt.x > 0 && listlistcd[i][j].pd.x > 0)){
                        Confusion_Matrix = (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "FP");
                    }else{
                        Confusion_Matrix = (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "FN");
                    }

                    if(listlistcd[i][j].OKSi == 0 && listlistcd[i][j].gt.x == 0 && listlistcd[i][j].gt.y == 0 &&
                    listlistcd[i][j].pd.x == 0 && listlistcd[i][j].pd.y == 0){
                        Confusion_Matrix = (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "TN");
                    }
                        
                    ComputeData re_cd = listlistcd[i][j];
                    re_cd.cMatrix[index] = Confusion_Matrix;
                    listlistcd[i][j] = re_cd;
                    }
                    index++;
                }                    
            }                
            APCalculate(listlistcd);                
        }
        //  순서 4. AP 계산(각 클래스별 AP계산)
        public void APCalculate(List<List<ComputeData>> list){       

            // 4-1. 키포인트별 리스트 저장순서를 뒤집으면서 사용할 변수 초기화
            //  기존 : 파일별리스트>구조체리스트(Nose,L.Eye,R.Eye,L.Ear,R.Ear.../Nose,L.Eye,R.Eye,L.Ear,R.Ear...)>구조체(Nose) 
            //  => 수정 : 키포인트별리스트>구조체리스트(Nose,Nose.../L.Eye,L.Eye...)>구조체(Nose)
            List<List<ComputeData>> tempcdList = new List<List<ComputeData>>();                
            for(int i =0; i<17; i++){
                List<ComputeData> templist = new List<ComputeData>();
                for(int j=0; j<list.Count; j++){
                    int flag = (int)list[j][i].keyname;
                    if(flag == i){
                        ComputeData cd = new ComputeData();
                        cd.keyname = list[j][i].keyname;
                        cd.filename = list[j][i].filename;
                        cd.dd = list[j][i].dd;
                        cd.ss = list[j][i].ss;
                        cd.gt = list[j][i].gt;
                        cd.pd = list[j][i].pd;
                        cd.OKSi = list[j][i].OKSi;
                        cd.precision = new float[4];
                        cd.recall = new float[4];
                        cd.cMatrix = list[j][i].cMatrix;
                        cd.fp = new int[4];
                        cd.tp = new int[4];
                        templist.Add(cd);
                    }                        
                }
                tempcdList.Add(templist);
            }

            
            // 4-2. 각 키포인트의 TP, FN 누적
            // recall은 모든 값이 나온 상태에서 계산되어야하므로 새로운 구조체에 담아 키포인트별로 리스트에 담아두기(0.5, 0.75, 0.9, 0.95순 저장)
            List<APComputeData> APList = new List<APComputeData>();
            for(int i =0; i<tempcdList.Count; i++){
                APComputeData apcd = new APComputeData();
                apcd.tpall = new int[4];
                apcd.fnall = new int[4];
                for(int j = 0; j<tempcdList[i].Count; j++){
                    int flag = (int)tempcdList[i][j].keyname;    
                    if(flag == i){      //keyname이 0/1/2/3/4.. 일때                           
                        apcd.keyname = tempcdList[i][j].keyname;
                        for(int z = 0; z<4; z++){
                            if(tempcdList[i][j].cMatrix[z] == (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "TP")){
                                apcd.tpall[z] += 1;
                            }else if(tempcdList[i][j].cMatrix[z] == (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "FN")){
                                apcd.fnall[z] += 1;
                            }
                        }                            
                    }
                }
                APList.Add(apcd);                    
            }

            List<List<ComputeData>> revercelistlistcd = new List<List<ComputeData>>();
            for(int i =0; i<tempcdList.Count; i++){
                List<ComputeData> revercelist = new List<ComputeData>();
                revercelist = tempcdList[i].OrderByDescending(x=>x.pd.score).ToList();
                revercelistlistcd.Add(revercelist);
            }

            // 4-3. 각 이미지별 TP와 FP의 누적치 계산
            // Precision 및 Recall을 계산하기 위해 각각의 키포인트 구조체에 누적된 TP와 FP를 계산하여 담기(0.5, 0.75, 0.9, 0.95순 저장)
            for(int i =0; i<revercelistlistcd.Count; i++){
                int[] tp = new int[4];
                int[] fp = new int[4];                    
                for(int j =0; j<revercelistlistcd[i].Count; j++){
                    int flag = (int)revercelistlistcd[i][j].keyname;  
                    if(flag == i){
                        ComputeData cd = revercelistlistcd[i][j];
                        for(int z=0; z<4; z++){
                            if(revercelistlistcd[i][j].cMatrix[z] == (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "TP")){
                                tp[z] += 1;
                            }else if(revercelistlistcd[i][j].cMatrix[z] == (CONFUSION_MATRIX)Enum.Parse(typeof(CONFUSION_MATRIX), "FP")){
                                fp[z] += 1;
                            }
                            cd.tp[z] = tp[z];
                            cd.fp[z] = fp[z];
                            revercelistlistcd[i][j] = cd;   
                        }                                                          
                    }
                }
            }

            // 4-4. 각 이미지별 Precision 및 Recall 계산
            // 첫 번째 이미지가 TP가 아닐 경우나 첫 번째로 나오는 TP가 없는 경우 Recall과 Precision이 계산될 수 없으므로 0입력
            // 그렇지 않은 경우는 계산하여 해당 구조체에 Precision 및 Recall을 담아둔다
            for(int i =0; i<revercelistlistcd.Count; i++){
                for(int j =0; j<revercelistlistcd[i].Count; j++){
                    float[] precision = new float[4];
                    float[] recall = new float[4];
                    int flag = (int)revercelistlistcd[i][j].keyname;
                    if(flag==i){
                        ComputeData cd = revercelistlistcd[i][j];
                        for(int z = 0; z<4 ; z++){
                            if(cd.tp[z] == 0){
                                precision[z] = 0.0f;
                                recall[z] = 0.0f;
                            }else{
                                precision[z] = (float)cd.tp[z]/(cd.tp[z]+cd.fp[z]);
                                recall[z] = (float)cd.tp[z]/(APList[i].tpall[z]+APList[i].fnall[z]);
                            }
                            cd.precision[z] = precision[z];
                            cd.recall[z] = recall[z];
                            revercelistlistcd[i][j] = cd;
                        }
                    }
                }
            }

            // 4-5. AP값 계산
            // 같은 recall 값일 때는 해당 recall 그룹의 MAX(Precision)으로 계산하여 모두 더한 후에 recall의 Unique 개수만큼 나누어준다
            List<double[]> aplist = new List<double[]>();
            for(int i=0; i<revercelistlistcd.Count; i++){
                //Nose, LeftEye, RightEye...
                double[] sum = new double[4];
                double[] ap = new double[4];
                for(int z =0; z<4; z++){
                    var groups = revercelistlistcd[i].GroupBy(g => g.recall[z]); //그룹화
                    var maxvalue = groups.Select(a=>a.Max(g=>g.precision[z]));;
                    foreach(var value in maxvalue){
                        sum[z] += value;                            
                    }
                    ap[z] = (double)sum[z]/groups.Count();
                }
                aplist.Add(ap);                                 
            }     

            mAPCalc(aplist);
        }

        //  순서 5. mAP 계산
        // 모든 클래스에 대한 mAP를 계산한다.
        // 0.5, 0.75, 0.9, 0.95에 해당하는 값 또한 들어있기 때문에 해당 값도 키포인트만큼 나누어 mAP 계산완료
        public void mAPCalc(List<double[]> aplist){
            float[] index = {0.5f, 0.75f, 0.9f, 0.95f};
            int flag = 0;

            for(int i =0; i<4; i++){
                double sum = 0.0;
                double map = 0.0;
                for(int j=0; j<aplist.Count; j++){
                    sum += aplist[j][i];
                }
                map = (double)sum/aplist.Count;
                // mAP 확인용
                Console.WriteLine("mAP(" + index[flag++] + ") : "+ Math.Round(map, 6));
            }
        }
    }
}