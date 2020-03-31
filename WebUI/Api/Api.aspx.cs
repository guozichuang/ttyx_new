using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Reflection;
using System.Configuration;
using System.Net;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Text;
using System.Data;
using System.Data.Common;
using Microsoft.Security.Application;

using Aop.Api;
using Aop.Api.Request;
using Aop.Api.Domain;
using Aop.Api.Response;
using Senparc.Weixin.MP;
using Senparc.Weixin.MP.TenPayLibV3;
using Senparc.Weixin.MP.AdvancedAPIs;

using LitJson;
using Simon.Common;

public partial class Api_Api : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        #region 通过反射执行方法
        Type type = this.GetType();
        MethodInfo mi = type.GetMethod(CurrSite.GetRdv(this, "act"), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (mi != null) mi.Invoke(this, null); //执行Method
        else SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "fail method"));
        #endregion
    }

    #region 微信公众号/微信登录/微信支付相关参数
    public static string WX_AppId
    {
        get { return CurrSite.GetAppSettings("TenPayV3_AppId"); }
    }
    public static string WX_AppSecret
    {
        get { return CurrSite.GetAppSettings("TenPayV3_AppSecret"); }
    }
    protected string WX_AccessToken
    {
        get { return Senparc.Weixin.MP.Containers.AccessTokenContainer.GetAccessToken(WX_AppId); }
    }
    private static TenPayV3Info _tenPayV3Info;
    public static TenPayV3Info TenPayV3Info
    {
        get
        {
            if (_tenPayV3Info == null)
            {
                _tenPayV3Info = TenPayV3InfoCollection.Data[CurrSite.GetAppSettings("TenPayV3_MchId")];
            }
            return _tenPayV3Info;
        }
    }
    #endregion


    #region 前端API验签检查(辅助方法)
    /// <summary>
    /// 前端API验签检查(辅助方法)
    /// </summary>
    private void CheckSign()
    {
        string t = SimonUtils.Qnum("t");  //unix时间戳 (10位数字)
        string sign = SimonUtils.Q("sign");  //签名

        if (t.Length != 10)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "时间戳错误"));
        }
        if (CurrSite.ApiCallTimeOut(t))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请求超时"));
        }
        if (!CurrSite.VerifySign(sign, t))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "签名错误"));
        }
    }
    #endregion

    #region 是否启用注册推荐人
    protected void EnableRegRec()
    {
        CheckSign();

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["enableregrec"] = CurrSite.EnableRegRec;
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 绑定微信
    protected void BindWeixin()
    {
        CheckSign();
        string wxopenid = Request.Params["wxopenid"];  //微信openid
        string wxnickname = Request.Params["wxnickname"];  //微信nickname
        string regip = Request.Params["regip"];  //注册IP
        string recuserid = SimonUtils.Qnum("recuserid");  //注册推荐人ID(数字类型)
        string headIconUrl = Request.Params["headIconUrl"];  //微信头像
        string gender = Request.Params["gender"];  //性别

        if (string.IsNullOrWhiteSpace(wxopenid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "wxopenid error"));
        }
        if (string.IsNullOrWhiteSpace(wxnickname))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "wxnickname error"));
        }
        if (string.IsNullOrWhiteSpace(regip) || regip.Length > 15 || !SimonUtils.IsIP(regip))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "注册IP错误"));
        }
        if (CurrSite.EnableRegRec && recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID错误"));
        }
        if (recuserid == "") recuserid = "0";

        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a left join Web_UserWeixin as b on a.UserID=b.UserID where b.WeixinOpenID is not null and b.WeixinOpenID<>'' and b.WeixinOpenID=@WeixinOpenID", new DbParameter[] {
            SimonDB.CreDbPar("@WeixinOpenID", wxopenid)
        });
        if (UserDT.Rows.Count <= 0)
        {
            //自动注册微信用户
            string _newusername = CurrSite.GenNewUserName(wxnickname);
            while (SimonDB.IsExist(@"select * from TUsers where UserName=@UserName", new DbParameter[] { SimonDB.CreDbPar("@UserName", _newusername) })
                   ||
                   SimonDB.IsExist(@"select * from TUsers where NickName=@NickName", new DbParameter[] { SimonDB.CreDbPar("@NickName", _newusername) })
                  )
            {
                _newusername = CurrSite.GenNewUserName(wxnickname);
            }

            string usermd5pwd = SimonUtils.EnCodeMD5(_newusername + DateTime.Now.ToString("fff"));
            List<DbParameter> reg_lpar = new List<DbParameter>();
            reg_lpar.Add(SimonDB.CreDbPar("@UserID", ParameterDirection.Output, 0));
            reg_lpar.Add(SimonDB.CreDbPar("@UserName", _newusername));
            reg_lpar.Add(SimonDB.CreDbPar("@Pass", usermd5pwd));
            //reg_lpar.Add(SimonDB.CreDbPar("@NickName", _newusername));
            reg_lpar.Add(SimonDB.CreDbPar("@NickName", wxnickname));    //去掉昵称后的数字
            reg_lpar.Add(SimonDB.CreDbPar("@TGName", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@TwoPassword", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@Sex", "1"));
            reg_lpar.Add(SimonDB.CreDbPar("@LogoId", "0"));
            reg_lpar.Add(SimonDB.CreDbPar("@ZJ_Number", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@RegisterIP", regip));
            reg_lpar.Add(SimonDB.CreDbPar("@PhoneNum", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@Email", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@QQNum", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@RealName", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@RecUserID", recuserid));
            int ReturnValue = SimonDB.RunProcedure("Web_pUsersAdd", reg_lpar.ToArray());
            object userid = reg_lpar[0].Value;
            if (userid != null && Convert.ToInt32(userid) > 0)
            {
                //写入UserID、WeixinOpenID绑定关系
                SimonDB.ExecuteNonQuery(@"insert into Web_UserWeixin (UserID,WeixinOpenID,BindingDateTime,HeadIconUrl,Gender) values (@UserID,@WeixinOpenID,getdate(),@HeadIconUrl,@Gender)", new DbParameter[] {
                    SimonDB.CreDbPar("@UserID", userid.ToString()),
                    SimonDB.CreDbPar("@WeixinOpenID", wxopenid),
                    SimonDB.CreDbPar("@HeadIconUrl",headIconUrl),
                    SimonDB.CreDbPar("@Gender",gender)
                });


                List<DbParameter> lpar = new List<DbParameter>();

                //赠送房卡CurrencyType=1（1为房卡，2为钻石，3为金币），DeductType=3（ 1为游戏消耗，2为管理员扣除，3为注册总送）涉及表：FangkaRecord、TUserInfo
                lpar.Add(SimonDB.CreDbPar("@userid", userid));
                lpar.Add(SimonDB.CreDbPar("@fangka", CurrSite.GetAppSettings("givefangka")));
                lpar.Add(SimonDB.CreDbPar("@RecordNum", DateTime.Now.ToString("yyyyMMddHHmmss") + userid));
                SimonDB.ExecuteNonQuery(@"update TUserInfo set RoomCard=@fangka where UserID=@userid", lpar.ToArray());
                SimonDB.ExecuteNonQuery(@"insert into FangkaRecord values(@userid, @RecordNum, 1, 0,@fangka, 3, GETDATE(), '微信注册赠送')", lpar.ToArray());

                lpar.Add(SimonDB.CreDbPar("@givegold", CurrSite.GetAppSettings("givegold")));
                SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=@givegold where UserID=@userid", lpar.ToArray());
                SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog ( UserID , UserName ,StartMoney ,ChangeMoney ,ChangeType,OpuserType,DateTime ,Remark ) values
             (@userid,@UserName,0,@givegold,26,0,getdate(),'注册赠送')", lpar.ToArray());

                JsonData jd = new JsonData();
                jd["code"] = "1";
                jd["msg"] = "success";
                jd["results"] = new JsonData();
                jd["results"]["userid"] = userid.ToString();
                jd["results"]["usermd5pwd"] = usermd5pwd;

                SimonUtils.RespWNC(jd.ToJson());
            }
            else SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "注册失败,请重试"));
        }
        else
        {
            DataRow UserDR = UserDT.Rows[0];

            SimonDB.ExecuteNonQuery("update Web_UserWeixin set WeixinOpenID=@WeixinOpenID, HeadIconUrl=@HeadIconUrl,Gender=@Gender where UserID=@UserID", new DbParameter[] {
                    SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
                    SimonDB.CreDbPar("@WeixinOpenID", wxopenid),
                    SimonDB.CreDbPar("@HeadIconUrl",headIconUrl),
                    SimonDB.CreDbPar("@Gender",gender)
                });
            SimonDB.ExecuteNonQuery("update TUsers set NickName=@NickName where UserID=@UserID", new DbParameter[] {
                    SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
                    SimonDB.CreDbPar("@NickName", wxnickname)
                });

            JsonData jd = new JsonData();
            jd["code"] = "1";
            jd["msg"] = "success";
            jd["results"] = new JsonData();
            jd["results"]["userid"] = UserDR["UserID"].ToString();
            jd["results"]["usermd5pwd"] = UserDR["Pass"].ToString();

            SimonUtils.RespWNC(jd.ToJson());
        }
    }
    #endregion

    #region 绑定闲聊
    protected void BindXianliao()
    {
        CheckSign();
        string xlopenid = Request.Params["xlopenid"];  //闲聊openid
        string xlnickname = Request.Params["xlnickname"];  //闲聊nickname
        string regip = Request.Params["regip"];  //注册IP
        string recuserid = SimonUtils.Qnum("recuserid");  //注册推荐人ID(数字类型)
        string headIconUrl = Request.Params["headIconUrl"];  //闲聊头像
        string gender = Request.Params["gender"];  //性别

        if (string.IsNullOrWhiteSpace(xlopenid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "xlopenid error"));
        }
        if (string.IsNullOrWhiteSpace(xlnickname))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "xlnickname error"));
        }
        if (string.IsNullOrWhiteSpace(regip) || regip.Length > 15 || !SimonUtils.IsIP(regip))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "注册IP错误"));
        }
        if (CurrSite.EnableRegRec && recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID错误"));
        }
        if (recuserid == "") recuserid = "0";

        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a left join Web_UserXianliao as b on a.UserID=b.UserID where b.XianliaoOpenID is not null and b.XianliaoOpenID<>'' and b.XianliaoOpenID=@XianliaoOpenID", new DbParameter[] {
            SimonDB.CreDbPar("@XianliaoOpenID", xlopenid)
        });
        if (UserDT.Rows.Count <= 0)
        {
            //自动注册闲聊用户
            string _newusername = CurrSite.GenNewUserName(xlnickname);
            while (SimonDB.IsExist(@"select * from TUsers where UserName=@UserName", new DbParameter[] { SimonDB.CreDbPar("@UserName", _newusername) })
                   ||
                   SimonDB.IsExist(@"select * from TUsers where NickName=@NickName", new DbParameter[] { SimonDB.CreDbPar("@NickName", _newusername) })
                  )
            {
                _newusername = CurrSite.GenNewUserName(xlnickname);
            }

            string usermd5pwd = SimonUtils.EnCodeMD5(_newusername + DateTime.Now.ToString("fff"));
            List<DbParameter> reg_lpar = new List<DbParameter>();
            reg_lpar.Add(SimonDB.CreDbPar("@UserID", ParameterDirection.Output, 0));
            reg_lpar.Add(SimonDB.CreDbPar("@UserName", _newusername));
            reg_lpar.Add(SimonDB.CreDbPar("@Pass", usermd5pwd));
            reg_lpar.Add(SimonDB.CreDbPar("@NickName", _newusername));
            reg_lpar.Add(SimonDB.CreDbPar("@TGName", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@TwoPassword", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@Sex", "1"));
            reg_lpar.Add(SimonDB.CreDbPar("@LogoId", "0"));
            reg_lpar.Add(SimonDB.CreDbPar("@ZJ_Number", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@RegisterIP", regip));
            reg_lpar.Add(SimonDB.CreDbPar("@PhoneNum", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@Email", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@QQNum", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@RealName", ""));
            reg_lpar.Add(SimonDB.CreDbPar("@RecUserID", recuserid));
            int ReturnValue = SimonDB.RunProcedure("Web_pUsersAdd", reg_lpar.ToArray());
            object userid = reg_lpar[0].Value;
            if (userid != null && Convert.ToInt32(userid) > 0)
            {
                //写入UserID、XianliaoOpenID绑定关系
                SimonDB.ExecuteNonQuery(@"insert into Web_UserXianliao (UserID,XianliaoOpenID,BindingDateTime,HeadIconUrl,Gender) values (@UserID,@XianliaoOpenID,getdate(),@HeadIconUrl,@Gender)", new DbParameter[] {
                    SimonDB.CreDbPar("@UserID", userid.ToString()),
                    SimonDB.CreDbPar("@XianliaoOpenID", xlopenid),
                    SimonDB.CreDbPar("@HeadIconUrl",headIconUrl),
                    SimonDB.CreDbPar("@Gender",gender)
                });


                List<DbParameter> lpar = new List<DbParameter>();

                //赠送房卡CurrencyType=1（1为房卡，2为钻石，3为金币），DeductType=3（ 1为游戏消耗，2为管理员扣除，3为注册总送）涉及表：FangkaRecord、TUserInfo
                lpar.Add(SimonDB.CreDbPar("@userid", userid));
                lpar.Add(SimonDB.CreDbPar("@fangka", CurrSite.GetAppSettings("givefangka")));
                lpar.Add(SimonDB.CreDbPar("@RecordNum", DateTime.Now.ToString("yyyyMMddHHmmss") + userid));
                SimonDB.ExecuteNonQuery(@"update TUserInfo set RoomCard=@fangka where UserID=@userid", lpar.ToArray());
                SimonDB.ExecuteNonQuery(@"insert into FangkaRecord values(@userid, @RecordNum, 1, 0,@fangka, 3, GETDATE(), '闲聊注册赠送')", lpar.ToArray());

                JsonData jd = new JsonData();
                jd["code"] = "1";
                jd["msg"] = "success";
                jd["results"] = new JsonData();
                jd["results"]["userid"] = userid.ToString();
                jd["results"]["usermd5pwd"] = usermd5pwd;

                SimonUtils.RespWNC(jd.ToJson());
            }
            else SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "注册失败,请重试"));
        }
        else
        {
            DataRow UserDR = UserDT.Rows[0];

            JsonData jd = new JsonData();
            jd["code"] = "1";
            jd["msg"] = "success";
            jd["results"] = new JsonData();
            jd["results"]["userid"] = UserDR["UserID"].ToString();
            jd["results"]["usermd5pwd"] = UserDR["Pass"].ToString();

            SimonUtils.RespWNC(jd.ToJson());
        }
    }
    #endregion

    #region 快捷注册
    protected void QuickReg()
    {
        CheckSign();
        string username = Request.Params["username"];  //用户名
        string userpwd = Request.Params["userpwd"];  //密码
        string nickname = Request.Params["nickname"];  //用户昵称
        string regip = Request.Params["regip"];  //注册IP
        string recuserid = SimonUtils.Qnum("recuserid");  //注册推荐人ID(数字类型)

        if (string.IsNullOrWhiteSpace(username) || SimonUtils.GetStrLen(username) < 4 || SimonUtils.GetStrLen(username) > 20)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户名长度4～20个字符"));
        }
        if (!SimonUtils.IsNumOrEn(username))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户名由字母或数字组成"));
        }
        if (string.IsNullOrWhiteSpace(userpwd) || userpwd.Length < 6 || userpwd.Length > 20)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "密码长度6～20个字符"));
        }
        if (!SimonUtils.IsNumOrEn(userpwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "密码由字母或数字组成"));
        }
        if (string.IsNullOrWhiteSpace(nickname) || SimonUtils.GetStrLen(nickname) < 4 || SimonUtils.GetStrLen(nickname) > 20)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户昵称长度4～20个字符"));
        }
        if (!SimonUtils.IsEC(nickname))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户昵称由字母、数字或中文组成"));
        }
        if (string.IsNullOrWhiteSpace(regip) || regip.Length > 15 || !SimonUtils.IsIP(regip))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "注册IP错误"));
        }
        if (CurrSite.EnableRegRec && recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID错误"));
        }
        if (recuserid == "") recuserid = "0";
        if ((int)SimonDB.ExecuteScalar(@"select count(*) from recuser where recuserid=@RecUserID", new DbParameter[] { SimonDB.CreDbPar(@"RecUserID", recuserid) }) <= 0)
        {
            recuserid = "0";
        }
        if (CurrSite.EnableRegRec && recuserid == "0")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人不存在"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@UserID", ParameterDirection.Output, 0));
        lpar.Add(SimonDB.CreDbPar("@UserName", username));
        lpar.Add(SimonDB.CreDbPar("@Pass", SimonUtils.EnCodeMD5(userpwd)));
        lpar.Add(SimonDB.CreDbPar("@NickName", nickname));
        lpar.Add(SimonDB.CreDbPar("@TGName", ""));
        lpar.Add(SimonDB.CreDbPar("@TwoPassword", ""));
        lpar.Add(SimonDB.CreDbPar("@Sex", "1"));
        lpar.Add(SimonDB.CreDbPar("@LogoId", "0"));
        lpar.Add(SimonDB.CreDbPar("@ZJ_Number", ""));
        //lpar.Add(SimonDB.CreDbPar("@RegisterIP", regip));
        lpar.Add(SimonDB.CreDbPar("@RegisterIP", SimonUtils.GetUserIp()));
        lpar.Add(SimonDB.CreDbPar("@PhoneNum", ""));
        lpar.Add(SimonDB.CreDbPar("@Email", ""));
        lpar.Add(SimonDB.CreDbPar("@QQNum", ""));
        lpar.Add(SimonDB.CreDbPar("@RealName", ""));
        lpar.Add(SimonDB.CreDbPar("@RecUserID", recuserid));

        int ReturnValue = SimonDB.RunProcedure("Web_pUsersAdd", lpar.ToArray());
        object userid = lpar[0].Value;

        if (ReturnValue.Equals(-1))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户名已存在"));
        }
        if (ReturnValue.Equals(-2))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "昵称已存在"));
        }

        if (userid != null && Convert.ToInt32(userid) > 0)
        {

            //增送金币26
            lpar.Add(SimonDB.CreDbPar("@userid", userid));
            lpar.Add(SimonDB.CreDbPar("@givegold", CurrSite.GetAppSettings("givegold")));
            SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=@givegold where UserID=@userid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog ( UserID , UserName ,StartMoney ,ChangeMoney ,ChangeType,OpuserType,DateTime ,Remark ) values
             (@UserID,@UserName,0,@givegold,26,0,getdate(),'注册赠送')", lpar.ToArray());

            //赠送房卡CurrencyType=1（1为房卡，2为钻石，3为金币），DeductType=3（ 1为游戏消耗，2为管理员扣除，3为注册总送）涉及表：FangkaRecord、TUserInfo
            //lpar.Add(SimonDB.CreDbPar("@userid", userid));
            lpar.Add(SimonDB.CreDbPar("@fangka", CurrSite.GetAppSettings("givefangka")));
            lpar.Add(SimonDB.CreDbPar("@RecordNum", DateTime.Now.ToString("yyyyMMddHHmmss") + userid));
            SimonDB.ExecuteNonQuery(@"update TUserInfo set RoomCard=@fangka where UserID=@userid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into FangkaRecord values(@userid, @RecordNum, 1, 0,@fangka, 3, GETDATE(), '快捷注册赠送')", lpar.ToArray());


            JsonData jd = new JsonData();
            jd["code"] = "1";
            jd["msg"] = "success";
            jd["results"] = new JsonData();
            jd["results"]["userid"] = userid.ToString();
            SimonUtils.RespWNC(jd.ToJson());
        }
        else SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "注册失败,请重试"));
    }
    #endregion

    #region 游戏公告
    protected void GetGameNotice()
    {
        CheckSign();
        string noticetype = SimonUtils.Qnum("noticetype");  //公告类型（0普通公告，1兑奖公告）

        if (noticetype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "公告类型错误"));
        }

        DataTable NoticeDT = SimonDB.DataTable(@"select * from gamenotice where noticetype=@noticetype order by adddate desc", new DbParameter[] { SimonDB.CreDbPar("@noticetype", noticetype) });
        if (NoticeDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "暂无公告"));
        }

        DataRow NoticeDR = NoticeDT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["noticetype"] = NoticeDR["NoticeType"].ToString();
        jd["results"]["noticecon"] = NoticeDR["NoticeCon"].ToString();
        jd["results"]["adddate"] = NoticeDR["AddDate"].ToString();
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 充值(15173)
    protected void Recharge()
    {
        CheckSign();
        string device = Request.Params["device"];   //设备信息:ios,android
        string device_id = Request.Params["device_id"];   //设备ID
        string player_ip = Request.Params["player_ip"];   //玩家IP
        string player_id = Request.Params["player_id"];   //玩家账号
        string pay_type = Request.Params["pay_type"];   //支付方式 支付宝: alipay, 微信: wxpay, 微信pc端: wxpay_pc
        string total_fee = Request.Params["total_fee"];   //充值金额(单位：元)

        if (string.IsNullOrWhiteSpace(device))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "设备信息错误"));
        }
        if (string.IsNullOrWhiteSpace(device_id))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "设备ID错误"));
        }
        if (string.IsNullOrWhiteSpace(player_ip))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家IP错误"));
        }
        if (string.IsNullOrWhiteSpace(player_id))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家账号错误"));
        }
        if (string.IsNullOrWhiteSpace(pay_type))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "支付方式错误"));
        }
        if (string.IsNullOrWhiteSpace(total_fee) || !SimonUtils.IsDecimal(total_fee))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值金额错误"));
        }

        //判断玩家账号是否存在
        DbParameter[] userparms = new DbParameter[] { SimonDB.CreDbPar("@userid", player_id) };
        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", userparms);
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];

        //判断用户是否在游戏中
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", userparms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", userparms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}

        //对接15173
        string to15173url = string.Empty;
        switch (pay_type.ToLower())
        {
            case "alipay": to15173url = CurrSite.Pay15173_toalipay_url; break;
            case "wxpay": to15173url = CurrSite.Pay15173_towxpay_url; break;
            case "wxpay_pc": to15173url = CurrSite.Pay15173_towxpay_pc_url; break;
            default: to15173url = CurrSite.Pay15173_towxpay_url; break;
        }
        if (to15173url.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "支付方式错误(仅支持支付宝、微信)"));
        }
        //提交参数整理(有顺序)
        string order_num = DateTime.Now.ToString("yyyyMMddHHmmssff") + player_id.PadRight(8, '0');
        string sendto_15173 = string.Empty;
        sendto_15173 += "bargainor_id=" + CurrSite.Pay15173_bargainor_id;
        sendto_15173 += "&sp_billno=" + order_num;
        sendto_15173 += "&pay_type=" + pay_type;
        sendto_15173 += "&return_url=" + CurrSite.Pay15173_return_url;
        sendto_15173 += "&attach=111";
        sendto_15173 += "&sign=" + SimonUtils.EnCodeMD5(sendto_15173 + "&key=" + CurrSite.Pay15173_key);
        sendto_15173 += "&total_fee=" + total_fee;
        sendto_15173 += "&select_url=" + CurrSite.Pay15173_select_url;
        sendto_15173 += "&zidy_code=" + player_id;
        sendto_15173 += "&czip=" + player_ip;
        //提交至15173
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(to15173url + (sendto_15173 == "" ? "" : "?") + sendto_15173);
        req.Method = "GET";
        req.ContentType = "text/html;charset=UTF-8";
        HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
        if ((int)resp.StatusCode != 200)
        {
            //写错误日志
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n 错误日志 支付动作-----------------------------------------------------------------------------------");
            sb.Append("\r\n StatusCode=" + resp.StatusCode.ToString());
            sb.Append("\r\n device=" + device);
            sb.Append("\r\n device_id=" + device_id);
            sb.Append("\r\n player_ip=" + player_ip);
            sb.Append("\r\n player_id=" + player_id);
            sb.Append("\r\n pay_type=" + pay_type);
            sb.Append("\r\n total_fee=" + total_fee);
            sb.Append("\r\n url_15173=" + to15173url);
            sb.Append("\r\n sendto_15173=" + sendto_15173);
            sb.Append("\r\n--------------------------------------------------------------------------------------------------");
            SimonLog.WriteLog(sb.ToString(), "/Log/", "log_15173_" + DateTime.Now.ToString("yyyyMMdd"));
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "通信错误:StatusCode=" + resp.StatusCode.ToString()));
        }
        Stream respStream = resp.GetResponseStream();
        StreamReader myStreamReader = new StreamReader(respStream, Encoding.UTF8);
        string retString = myStreamReader.ReadToEnd();
        myStreamReader.Close();
        respStream.Close();

        //创建订单
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@Users_ids", UserDR["UserID"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@TrueName", UserDR["NickName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@PayMoney", total_fee));
        lpar.Add(SimonDB.CreDbPar("@PayType", "73"));  //15173 的支付类型设置为73
        lpar.Add(SimonDB.CreDbPar("@TypeInfo", "15173_" + pay_type));
        lpar.Add(SimonDB.CreDbPar("@OrderID", order_num));  //订单号
        lpar.Add(SimonDB.CreDbPar("@AddTime", DateTime.Now.ToString()));
        lpar.Add(SimonDB.CreDbPar("@ExchangeRate", "1"));  //充值兑换率(此字段暂时无效)
        lpar.Add(SimonDB.CreDbPar("@InMoney", "0"));  //提交订单时写入0,确定充值成功后需更新该字段
        lpar.Add(SimonDB.CreDbPar("@InSuccess", false));
        lpar.Add(SimonDB.CreDbPar("@PaySuccess", false));
        lpar.Add(SimonDB.CreDbPar("@MoneyFront", UserDR["WalletMoney"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "0"));  //更新状态
        lpar.Add(SimonDB.CreDbPar("@PurchaseType", "1")); //充值金币1 充值元宝2
        lpar.Add(SimonDB.CreDbPar("@PayIP", player_ip));
        lpar.Add(SimonDB.CreDbPar("@ao_device", device));
        lpar.Add(SimonDB.CreDbPar("@ao_device_id", device_id));
        SimonDB.ExecuteNonQuery(@"insert into Web_RMBCost  (Users_ids,TrueName,UserName,PayMoney,PayType,TypeInfo,OrderID,AddTime,
                                                            ExchangeRate,InMoney,InSuccess,PaySuccess,MoneyFront,UpdateFlag,PurchaseType,
                                                            PayIP,ao_device,ao_device_id)
                                                    values (@Users_ids,@TrueName,@UserName,@PayMoney,@PayType,@TypeInfo,@OrderID,@AddTime,
                                                            @ExchangeRate,@InMoney,@InSuccess,@PaySuccess,@MoneyFront,@UpdateFlag,@PurchaseType,
                                                            @PayIP,@ao_device,@ao_device_id)", lpar.ToArray());

        SimonUtils.RespWNC(retString);  //输出支付页面h5代码
    }
    #endregion

    #region 充值(竣付通)
    protected void JftRecharge()
    {
        CheckSign();
        string p25_termina = Request.Params["pay_device"];   //设备信息:1 代表 pc   2 代表 ios  3 代表 android。
        string p16_customip = Request.Params["player_ip"];   //玩家IP,格式“198_0_0_1”
        string player_id = Request.Params["player_id"];   //玩家账号
        string p7_productcode = Request.Params["pay_type"];   //支付方式 支付宝: ZFB, 微信: WX
        string p3_money = Request.Params["pay_money"];   //充值金额(单位：元)
        //string p2_ordernumber= Request.Params["pay_ordernumber"];

        if (string.IsNullOrWhiteSpace(p25_termina))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "设备信息错误"));
        }
        if (string.IsNullOrWhiteSpace(p16_customip))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家IP错误"));
        }
        if (string.IsNullOrWhiteSpace(player_id))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家账号错误"));
        }
        if (string.IsNullOrWhiteSpace(p7_productcode))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "支付方式错误"));
        }
        if (string.IsNullOrWhiteSpace(p3_money) || !SimonUtils.IsDecimal(p3_money))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值金额错误"));
        }

        //判断玩家账号是否存在
        DbParameter[] userparms = new DbParameter[] { SimonDB.CreDbPar("@userid", player_id) };
        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", userparms);
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];

        //判断用户是否在游戏中
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", userparms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", userparms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}


        //对接竣付通
        string toJft_Url = CurrSite.jft_post_url;
        //提交参数整理(有顺序)
        string p2_ordernumber = DateTime.Now.ToString("yyyyMMddHHmmssff") + player_id.PadRight(8, '0');
        string sendto_Jft = string.Empty;
        sendto_Jft += "p1_yingyongnum=" + CurrSite.jft_yingyongnum;
        sendto_Jft += "&p2_ordernumber=" + p2_ordernumber;
        sendto_Jft += "&p3_money=" + p3_money;
        sendto_Jft += "&p6_ordertime=" + DateTime.Now.ToString("yyyyMMddHHmmss");
        sendto_Jft += "&p7_productcode=" + p7_productcode;
        sendto_Jft += "&p8_sign=" + SimonUtils.EnCodeMD5(CurrSite.jft_yingyongnum + "&" + p2_ordernumber + "&" + p3_money + "&" + DateTime.Now.ToString("yyyyMMddHHmmss") + "&" + p7_productcode + "&" + CurrSite.jft_compkey);
        sendto_Jft += "&p9_signtype=1";
        sendto_Jft += "&p10_bank_card_code=";
        sendto_Jft += "&p11_cardtype=";
        sendto_Jft += "&p12_channel=";
        sendto_Jft += "&p13_orderfailertime=";
        sendto_Jft += "&p14_customname=" + player_id;
        sendto_Jft += "&p15_customcontact=";
        sendto_Jft += "&p16_customip=" + p16_customip;
        sendto_Jft += "&p17_product=房卡";
        sendto_Jft += "&p18_productcat=";
        sendto_Jft += "&p19_productnum=";
        sendto_Jft += "&p20_pdesc=";
        sendto_Jft += "&p21_version=";
        sendto_Jft += "&p22_sdkversion=";
        sendto_Jft += "&p23_charset=";
        sendto_Jft += "&p24_remark=";
        sendto_Jft += "&p25_terminal=" + p25_termina;
        sendto_Jft += "&p26_ext1=1.1";
        sendto_Jft += "&p27_ext2=";
        sendto_Jft += "&p28_ext3=";
        sendto_Jft += "&p29_ext4=";
        //提交至竣付通
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(toJft_Url + (sendto_Jft == "" ? "" : "?") + sendto_Jft);
        req.Method = "post";
        req.ContentType = "text/html;charset=UTF-8";
        HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
        if ((int)resp.StatusCode != 200)
        {
            //写错误日志
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n 错误日志 支付动作-----------------------------------------------------------------------------------");
            sb.Append("\r\n StatusCode=" + resp.StatusCode.ToString());
            sb.Append("\r\n device=" + p25_termina);
            sb.Append("\r\n player_ip=" + p16_customip);
            sb.Append("\r\n player_id=" + player_id);
            sb.Append("\r\n pay_type=" + p7_productcode);
            sb.Append("\r\n money=" + p3_money);
            sb.Append("\r\n url_Jft=" + toJft_Url);
            sb.Append("\r\n sendto_Jft=" + sendto_Jft);
            sb.Append("\r\n--------------------------------------------------------------------------------------------------");
            SimonLog.WriteLog(sb.ToString(), "/Log/", "log_jft_" + DateTime.Now.ToString("yyyyMMdd"));
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "通信错误:StatusCode=" + resp.StatusCode.ToString()));
        }
        Stream respStream = resp.GetResponseStream();
        StreamReader myStreamReader = new StreamReader(respStream, Encoding.UTF8);
        string retString = myStreamReader.ReadToEnd();
        myStreamReader.Close();
        respStream.Close();

        //创建订单
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@Users_ids", UserDR["UserID"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@TrueName", UserDR["NickName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@PayMoney", p3_money));
        lpar.Add(SimonDB.CreDbPar("@PayType", "76"));  //15173 的支付类型设置为76
        lpar.Add(SimonDB.CreDbPar("@TypeInfo", "Jft_" + p7_productcode));
        lpar.Add(SimonDB.CreDbPar("@OrderID", p2_ordernumber));  //订单号
        lpar.Add(SimonDB.CreDbPar("@AddTime", DateTime.Now.ToString()));
        lpar.Add(SimonDB.CreDbPar("@ExchangeRate", "1"));  //充值兑换率(此字段暂时无效)
        lpar.Add(SimonDB.CreDbPar("@InMoney", "0"));  //提交订单时写入0,确定充值成功后需更新该字段
        lpar.Add(SimonDB.CreDbPar("@InSuccess", false));
        lpar.Add(SimonDB.CreDbPar("@PaySuccess", false));
        lpar.Add(SimonDB.CreDbPar("@MoneyFront", UserDR["WalletMoney"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "0"));  //更新状态
        lpar.Add(SimonDB.CreDbPar("@PurchaseType", "1")); //充值金币1 充值元宝2
        lpar.Add(SimonDB.CreDbPar("@PayIP", p16_customip));
        lpar.Add(SimonDB.CreDbPar("@ao_device", p25_termina));
        SimonDB.ExecuteNonQuery(@"insert into Web_RMBCost  (Users_ids,TrueName,UserName,PayMoney,PayType,TypeInfo,OrderID,AddTime,
                                                            ExchangeRate,InMoney,InSuccess,PaySuccess,MoneyFront,UpdateFlag,PurchaseType,
                                                            PayIP,ao_device,ao_device_id)
                                                    values (@Users_ids,@TrueName,@UserName,@PayMoney,@PayType,@TypeInfo,@OrderID,@AddTime,
                                                            @ExchangeRate,@InMoney,@InSuccess,@PaySuccess,@MoneyFront,@UpdateFlag,@PurchaseType,
                                                            @PayIP,@ao_device,'')", lpar.ToArray());

        SimonUtils.RespWNC(retString);  //输出支付页面h5代码
    }
    #endregion

    #region 获取订单支付状态
    protected void GetPayStatus()
    {
        CheckSign();
        string order_num = Request.Params["order_num"];   //订单号

        if (string.IsNullOrWhiteSpace(order_num) || order_num.Length != 25 || !SimonUtils.IsNum(order_num))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单号错误"));
        }

        //WebPayBll biz = new WebPayBll();
        //var paystatus = biz.IsUpdateOrder(order_num);
        //jd.Clear();
        //jd["code"] = "1";
        //jd["msg"] = "success";
        //jd["results"] = new JsonData();
        //jd["results"]["paystatus"] = paystatus;

        //SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 游戏币排行榜   修改 加 boss名 和倍率 列   修改为调用 LogHitFish 表
    protected void GetMoneyRankList()
    {
        CheckSign();
        string gameid = SimonUtils.Qnum("gameid");  //游戏ID    大话捕鱼70030600，摇钱树70611800，渔乐圈70661800
        string days = SimonUtils.Qnum("days");  //统计天数
        string topcount = SimonUtils.Qnum("topcount");  //排行榜统计数量

        if (gameid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏ID错误"));
        }
        if (days.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "统计天数错误"));
        }
        if (topcount.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "排行榜统计数量错误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@gameid", gameid));
        lpar.Add(SimonDB.CreDbPar("@days", days));

        string sqlstr = string.Format(@"
select top {0} rank()over(order by d.Score desc, d.CollectDateTime desc,d.UserID desc) as PrizeGrade,d.UserID,f.UserName,f.NickName,d.RoomName,d.Desk,d.FishName,d.BeiLv,d.Cell,d.Vip,d.Score as Score,d.CollectDateTime
from
(
	select c.UserID,c.Score,max(c.CollectDateTime) as CollectDateTime,c.RoomName,c.Desk,c.FishName,c.BeiLv,c.Vip,c.Cell
		from
		(
			select  a.UserID,a.Score ,a.CollectDateTime,a.RoomName,a.Desk,a.FishName,a.BeiLv,a.Vip,a.Cell 
				from LogHitFish a ,
				( 
					select  max(Score)as Score,UserID from LogHitFish with(nolock)
				    where datediff(day,collectdatetime,getdate())<=@days and gamenameid=@gameid
					group by UserID 
				)b
			where a.UserID=b.UserID and a.Score=b.Score
		)c
    group by c.UserID ,c.Score,c.RoomName,c.Desk,c.FishName,c.BeiLv,c.Vip,c.Cell
)d 
inner join LogHitFish e with(nolock) on e.UserID=d.UserID and e.Score =d.Score and e.CollectDateTime=d.CollectDateTime
inner join TUsers f on f.UserID=d.UserID", topcount);


        DataTable ListDT = SimonDB.DataTable(sqlstr, lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {

            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("moneyrank", DR["PrizeGrade"].ToString());
            tempdic.Add("changemoney", DR["Score"].ToString());
            tempdic.Add("bossname", DR["FishName"].ToString());
            tempdic.Add("beilv", DR["BeiLv"].ToString());
            tempdic.Add("collectdatetime", DateTime.Parse(DR["CollectDateTime"].ToString()).ToString("yyyy-MM-dd HH:mm"));
            tempdic.Add("collectdate", DateTime.Parse(DR["CollectDateTime"].ToString()).ToString("yyyy-MM-dd"));
            tempdic.Add("collecttime", DateTime.Parse(DR["CollectDateTime"].ToString()).ToString("HH:mm"));
            tempdic.Add("userid", DR["UserID"].ToString());
            tempdic.Add("username", DR["UserName"].ToString());
            tempdic.Add("nickname", DR["NickName"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 财富排行榜
    protected void GetAssetRankList()
    {
        CheckSign();
        string topcount = SimonUtils.Qnum("topcount");  //排行榜统计数量
        string ranktype = SimonUtils.Qnum("ranktype");  //财务榜类型 0 金币排行 1 奖卷排行

        if (topcount.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "排行榜统计数量错误"));
        }
        if (ranktype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "财务榜类型错误"));
        }

        string orderby = "WalletMoney desc";
        if (ranktype == "1") orderby = "lotteries desc";
        string sqlstr = string.Format(@"SELECT TOP {0} RANK() OVER (ORDER BY {1}) AS AssetRank,UserID,UserName,NickName,AllMoney,WalletMoney,BankMoney,Lotteries
                                        FROM 
                                        (
                                            select a.*,(b.WalletMoney+b.BankMoney) as AllMoney,b.WalletMoney,b.BankMoney,c.Lotteries 
                                            from TUsers as a 
                                            inner join TUserInfo as b on a.userid=b.userid
                                            inner join Web_Users as c on a.userid=c.userid
                                            where a.IsRobot=0
                                        ) as tb", topcount, orderby);
        DataTable ListDT = SimonDB.DataTable(sqlstr);

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("assetrank", DR["AssetRank"].ToString());
            tempdic.Add("allmoney", DR["AllMoney"].ToString());
            tempdic.Add("walletmoney", DR["WalletMoney"].ToString());
            tempdic.Add("bankmoney", DR["BankMoney"].ToString());
            tempdic.Add("lotteries", DR["Lotteries"].ToString());
            tempdic.Add("userid", DR["UserID"].ToString());
            tempdic.Add("username", DR["UserName"].ToString());
            tempdic.Add("nickname", DR["NickName"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 兑奖(下分下金币)订单(下单即扣金币) 
    protected void CashPrizeOrder()
    {
        CheckSign();
        string coinamount = SimonUtils.Qnum("coinamount");  //兑奖额度
        string remark = Sanitizer.GetSafeHtmlFragment(Request.Params["remark"]);  //备注信息
        string userid = SimonUtils.Qnum("userid");  //用户ID
        string userpwd = Request.Params["userpwd"];  //游戏密码(登录密码)(明文)

        if (!CurrSite.EnableCashPrize)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "兑奖功能已关闭"));
        }
        if (coinamount.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请输入兑奖额度"));
        }
        if (remark.Length > 200)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "备注信息0-200个字符"));
        }
        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误"));
        }
        if (string.IsNullOrWhiteSpace(userpwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请输入游戏密码"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@userpwd", SimonUtils.EnCodeMD5(userpwd)));

        DataTable UserDT = SimonDB.DataTable(@"select a.*, b.WalletMoney from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid and a.pass=@userpwd", lpar.ToArray());
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏密码错误"));
        }
        DataRow UserDR = UserDT.Rows[0];
        if (long.Parse(coinamount) > long.Parse(UserDR["WalletMoney"].ToString()))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "兑奖额度不能大于钱包金币数"));
        }

        //判断用户是否在游戏中
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", lpar.ToArray()) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", lpar.ToArray()) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}

        //扣除相应金币系列动作
        lpar.Add(SimonDB.CreDbPar("@username", UserDR["username"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@coinamount", coinamount));
        lpar.Add(SimonDB.CreDbPar("@remark", remark));
        lpar.Add(SimonDB.CreDbPar("@state", "0"));
        lpar.Add(SimonDB.CreDbPar("@adddate", DateTime.Now.ToString()));

        lpar.Add(SimonDB.CreDbPar("@changemoney", -long.Parse(coinamount)));
        lpar.Add(SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()));
        //扣除金币
        SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@changemoney where userid=@userid", lpar.ToArray());
        //写入金币变化日志
        SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
                                                          values (@UserID,@UserName,@StartMoney,@ChangeMoney,9,getdate(),'兑奖(下分)订单-修改金币数')", lpar.ToArray());
        //插入兑奖(下分)订单
        SimonDB.ExecuteNonQuery(@"insert into cashprizeorder (userid,username,coinamount,remark,state,adddate) values (@userid,@username,@coinamount,@remark,@state,@adddate)", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["userid"] = userid;
        jd["results"]["coinamount"] = coinamount;
        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    #region 兑奖(下分)订单列表(历史记录)
    protected void CashPrizeOrderList()
    {
        CheckSign();
        string topcount = SimonUtils.Qnum("topcount");  //获取记录数量
        string userid = SimonUtils.Qnum("userid");  //用户ID

        if (topcount.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "获取记录数量错误"));
        }
        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误"));
        }

        DbParameter[] parms = new DbParameter[] { SimonDB.CreDbPar("@userid", userid) };
        string _listsql = string.Format(@"select top {0} * from CashPrizeOrder where userid=@userid order by id desc", topcount);
        DataTable ListDT = SimonDB.DataTable(_listsql, parms);

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("id", DR["id"].ToString());
            tempdic.Add("userid", DR["userid"].ToString());
            tempdic.Add("username", DR["username"].ToString());
            tempdic.Add("coinamount", DR["coinamount"].ToString());
            tempdic.Add("remark", DR["remark"].ToString());
            tempdic.Add("state", DR["state"].ToString());
            tempdic.Add("adddate", DR["adddate"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取用户当前金币数量、房卡数量、奖卷数量
    protected void GetUserCurrCoin()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid");  //用户ID

        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误"));
        }

        DbParameter[] parms = new DbParameter[] { SimonDB.CreDbPar("@userid", userid) };
        DataTable UserDT = SimonDB.DataTable(@"select a.*, b.WalletMoney,b.RoomCard, c.Lotteries 
                                               from TUsers as a 
                                               inner join TUserInfo as b on a.userid=b.userid
                                               inner join Web_Users as c on a.userid=c.userid
                                               where a.userid=@userid", parms);
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["userid"] = userid;
        jd["results"]["usercurrcoin"] = UserDR["WalletMoney"].ToString();
        jd["results"]["userroomcard"] = UserDR["RoomCard"].ToString();
        jd["results"]["usercurrlotteries"] = UserDR["Lotteries"].ToString();
        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    #region 获取分享链接和二维码URL
    protected void GetShareLinkQRCode()
    {
        CheckSign();

        DataTable ShareLinkQRCodeDT = SimonDB.DataTable(@"select * from ShareLinkQRCode where isenable=1");
        DataRow ShareLinkQRCodeDR = ShareLinkQRCodeDT.Rows[0];

        string shareqrcode = string.Empty;
        if (ShareLinkQRCodeDR["shareqrcode"].ToString().IndexOf("http://") > -1)
            shareqrcode = ShareLinkQRCodeDR["shareqrcode"].ToString();
        else shareqrcode = string.Format("http://{0}{1}", Request.Url.Authority, ShareLinkQRCodeDR["shareqrcode"].ToString().ToLower());

        string sharepic = string.Empty;
        if (ShareLinkQRCodeDR["sharepic"].ToString().IndexOf("http://") > -1)
            sharepic = ShareLinkQRCodeDR["sharepic"].ToString();
        else sharepic = string.Format("http://{0}{1}", Request.Url.Authority, ShareLinkQRCodeDR["sharepic"].ToString().ToLower());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["sharetype"] = ShareLinkQRCodeDR["sharetype"].ToString();
        jd["results"]["sharetitle"] = ShareLinkQRCodeDR["sharetitle"].ToString();
        jd["results"]["sharedes"] = ShareLinkQRCodeDR["sharedes"].ToString();
        jd["results"]["sharecon"] = ShareLinkQRCodeDR["sharecon"].ToString();
        jd["results"]["link"] = ShareLinkQRCodeDR["sharelink"].ToString();
        jd["results"]["qrcodeurl"] = shareqrcode;
        jd["results"]["sharepic"] = sharepic;
        jd["results"]["isenable"] = ShareLinkQRCodeDR["isenable"].ToString();
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 是否启用 兑奖(下分订单功能)
    protected void EnableCashPrize()
    {
        CheckSign();

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["enablecashprize"] = CurrSite.EnableCashPrize;
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    //#region 奖卷兑换商品列表(分页)
    //protected void ExchangeGoodsList()
    //{
    //    CheckSign();
    //    string pageindex = SimonUtils.Qnum("pageindex");  //页码
    //    string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
    //    string type = SimonUtils.Qnum("type");  //(非必选)商品类型 1、金币；2、话费；3实物；
    //    string kw = Request.Params["kw"];  //(非必选)查询关键字 模糊匹配 title、des

    //    if (pageindex.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
    //    }
    //    if (pagesize.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
    //    }

    //    List<DbParameter> lpar = new List<DbParameter>();
    //    List<string> lwhere = new List<string>();

    //    if (type.Length > 0)
    //    {
    //        lpar.Add(SimonDB.CreDbPar("@type", type));
    //        lwhere.Add("type=@type");
    //    }
    //    if (!string.IsNullOrWhiteSpace(kw))
    //    {
    //        lpar.Add(SimonDB.CreDbPar("@kw", kw + "%"));
    //        lwhere.Add("(title like @kw or des like @kw)");
    //    }

    //    string _countsql = @"select count(1) from ExchangeGoods {0}";
    //    string _listsql = @"select * from (
    //                                select row_number() over (order by sort asc, id desc) as row, * from ExchangeGoods {0}
    //                            ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

    //    if (lwhere.Count > 0)
    //    {
    //        string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
    //        _countsql = string.Format(_countsql, _sqlwhere);
    //        _listsql = string.Format(_listsql, _sqlwhere);
    //    }
    //    else
    //    {
    //        _countsql = string.Format(_countsql, string.Empty);
    //        _listsql = string.Format(_listsql, string.Empty);
    //    }

    //    int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
    //    int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
    //    if (pageindex == "0") pageindex = "1";  //默认第1页
    //    if (pagesize == "0") pagesize = "10";  //默认每页10条
    //    if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

    //    lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
    //    lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

    //    DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
    //    List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
    //    foreach (DataRow DR in ListDT.Rows)
    //    {
    //        Dictionary<string, string> tempdic = new Dictionary<string, string>();
    //        tempdic.Add("id", DR["id"].ToString());  //商品id
    //        tempdic.Add("type", DR["type"].ToString());  //商品类型 1、金币；2、话费；3实物；
    //        tempdic.Add("title", DR["title"].ToString());  //商品标题
    //        tempdic.Add("img", DR["img"].ToString());  //商品标题图片url
    //        tempdic.Add("des", DR["des"].ToString());  //商品描述
    //        tempdic.Add("inventory", DR["inventory"].ToString());  //商品库存
    //        tempdic.Add("prizeprice", DR["prizeprice"].ToString());  //商品奖卷兑换价格
    //        tempdic.Add("exchangecoin", DR["exchangecoin"].ToString());  //兑换金币额
    //        tempdic.Add("exchangemobilefee", DR["exchangemobilefee"].ToString());  //兑换话费额
    //        tempdic.Add("givecoin", DR["givecoin"].ToString());  //附赠金币额
    //        tempdic.Add("sort", DR["sort"].ToString());  //排序
    //        tempdic.Add("updatetime", DR["updatetime"].ToString());  //更新时间

    //        resultslist.Add(tempdic);
    //    }

    //    Dictionary<string, object> jsondic = new Dictionary<string, object>();
    //    jsondic.Add("code", "1");
    //    jsondic.Add("msg", "success");
    //    jsondic.Add("recordcount", RecordCount.ToString());
    //    jsondic.Add("totalpage", TotalPage.ToString());
    //    jsondic.Add("pagesize", pagesize);
    //    jsondic.Add("pageindex", pageindex);
    //    jsondic.Add("results", resultslist);

    //    SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    //}
    //#endregion

    //#region 奖卷兑换商品详情
    //protected void ExchangeGoodsDetails()
    //{
    //    CheckSign();
    //    string id = SimonUtils.Qnum("id");  //商品ID

    //    if (id.Length < 1 || !SimonUtils.IsNum(id))
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品ID错误"));
    //    }

    //    DataTable DT = SimonDB.DataTable(@"select * from ExchangeGoods where id=@id", new DbParameter[] { SimonDB.CreDbPar("@id", id) });
    //    if (DT.Rows.Count <= 0)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品不存在"));
    //    }

    //    DataRow DR = DT.Rows[0];

    //    JsonData jd = new JsonData();
    //    jd["code"] = "1";
    //    jd["msg"] = "success";
    //    jd["results"] = new JsonData();
    //    jd["results"]["id"] = DR["id"].ToString();
    //    jd["results"]["type"] = DR["type"].ToString();
    //    jd["results"]["title"] = DR["title"].ToString();
    //    jd["results"]["img"] = DR["img"].ToString();
    //    jd["results"]["des"] = DR["des"].ToString();
    //    jd["results"]["inventory"] = DR["inventory"].ToString();
    //    jd["results"]["prizeprice"] = DR["prizeprice"].ToString();
    //    jd["results"]["exchangecoin"] = DR["exchangecoin"].ToString();
    //    jd["results"]["exchangemobilefee"] = DR["exchangemobilefee"].ToString();
    //    jd["results"]["givecoin"] = DR["givecoin"].ToString();
    //    jd["results"]["sort"] = DR["sort"].ToString();
    //    jd["results"]["updatetime"] = DR["updatetime"].ToString();

    //    SimonUtils.RespWNC(jd.ToJson());
    //}
    //#endregion

    //#region 奖卷兑换订单(下单即扣奖卷)
    //protected void ExchangeOrder()
    //{
    //    CheckSign();
    //    string userid = SimonUtils.Qnum("userid"); //用户ID
    //    string goodsid = SimonUtils.Qnum("goodsid"); //商品ID
    //    string realname = Request.Params["realname"]; //姓名1-200个字符(实物商品必填项)
    //    string mobile = Request.Params["mobile"]; //手机号1-50个字符(实物商品必填项)
    //    string address = Request.Params["address"]; //地址(实物商品必填项)

    //    if (userid.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
    //    }
    //    if (goodsid.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品ID错误(数字类型)"));
    //    }

    //    //读取商品信息
    //    DataTable GoodsDT = SimonDB.DataTable(@"select * from ExchangeGoods where id=@id", new DbParameter[] { SimonDB.CreDbPar("@id", goodsid) });
    //    if (GoodsDT.Rows.Count <= 0)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品不存在"));
    //    }
    //    DataRow GoodsDR = GoodsDT.Rows[0];
    //    //库存数值
    //    int result_inventory = int.Parse(GoodsDR["inventory"].ToString());
    //    //当兑换话费商品时先从话费卡数据表核对一下相应面值的话费卡库存数量并更新至商品表
    //    if (GoodsDR["type"].ToString().Equals("2"))
    //    {
    //        result_inventory = (int)SimonDB.ExecuteScalar(@"select count(1) from MobileFeeCard where mobilefee=@mobilefee and isgrant=0", new DbParameter[] {
    //            SimonDB.CreDbPar("@mobilefee", GoodsDR["exchangemobilefee"].ToString())
    //        });
    //        SimonDB.ExecuteNonQuery(@"update ExchangeGoods set inventory=@inventory where id=@id", new DbParameter[] {
    //            SimonDB.CreDbPar("@inventory", result_inventory),
    //            SimonDB.CreDbPar("@id", goodsid)
    //        });
    //    }

    //    //检查库存
    //    if (result_inventory <= 0)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品库存不足"));
    //    }

    //    //读取用户信息
    //    DbParameter[] user_parms = new DbParameter[] { SimonDB.CreDbPar("@userid", userid) };
    //    DataTable UserDT = SimonDB.DataTable(@"select a.*, b.WalletMoney, c.Lotteries 
    //                                           from TUsers as a 
    //                                           inner join TUserInfo as b on a.userid=b.userid
    //                                           inner join Web_Users as c on a.userid=c.userid
    //                                           where a.userid=@userid", user_parms);
    //    if (UserDT.Rows.Count <= 0)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
    //    }
    //    DataRow UserDR = UserDT.Rows[0];

    //    if (long.Parse(GoodsDR["prizeprice"].ToString()) > long.Parse(UserDR["Lotteries"].ToString()))
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您的奖卷不足,无法兑换该商品"));
    //    }

    //    //判断用户是否在游戏中
    //    //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", user_parms) > 0)
    //    //{
    //    //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
    //    //}
    //    //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", user_parms) > 0)
    //    //{
    //    //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
    //    //}

    //    if (GoodsDR["type"].ToString().Equals("3")) //实物商品
    //    {
    //        if (string.IsNullOrWhiteSpace(realname) || realname.Length > 200)
    //        {
    //            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "姓名1-200个字符(实物商品必填项)"));
    //        }
    //        if (string.IsNullOrWhiteSpace(mobile) || mobile.Length > 50)
    //        {
    //            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "手机号1-50个字符(实物商品必填项)"));
    //        }
    //        if (string.IsNullOrWhiteSpace(address))
    //        {
    //            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "地址错误(实物商品必填项)"));
    //        }
    //    }

    //    //扣除奖卷
    //    SimonDB.ExecuteNonQuery(@"update Web_Users set Lotteries=(Lotteries-@ChangeLotteries) where UserID=@UserID", new DbParameter[] {
    //        SimonDB.CreDbPar("@UserID", userid),
    //        SimonDB.CreDbPar("@ChangeLotteries", GoodsDR["prizeprice"].ToString())
    //    });
    //    //写入奖卷变化日志
    //    SimonDB.ExecuteNonQuery(@"insert into LogLotteries (UserId,PreLotteries,ChangeLotteries,CurLotteries,CollectDate)
    //                                             values (@UserId,@PreLotteries,@ChangeLotteries,@CurLotteries,GETDATE())", new DbParameter[] {
    //        SimonDB.CreDbPar("@UserId", userid),
    //        SimonDB.CreDbPar("@PreLotteries", UserDR["Lotteries"].ToString()),
    //        SimonDB.CreDbPar("@ChangeLotteries", GoodsDR["prizeprice"].ToString()),
    //        SimonDB.CreDbPar("@CurLotteries", long.Parse(UserDR["Lotteries"].ToString()) - long.Parse(GoodsDR["prizeprice"].ToString()))
    //    });
    //    //订单备注信息(不同类型商品标记备注信息不同)
    //    string orderremark = string.Empty;
    //    //奖卷兑换金币,金币实时到账
    //    if (GoodsDR["type"].ToString().Equals("1"))
    //    {
    //        //增加金币
    //        SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@changemoney where userid=@userid", new DbParameter[] {
    //            SimonDB.CreDbPar("@changemoney", long.Parse(GoodsDR["exchangecoin"].ToString())),
    //            SimonDB.CreDbPar("@userid", userid)
    //        });
    //        //写入金币变化日志
    //        SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
    //                                                          values (@UserID,@UserName,@StartMoney,@ChangeMoney,10,getdate(),'奖卷兑换金币,金币实时到账')", new DbParameter[] {
    //            SimonDB.CreDbPar("@UserID", userid),
    //            SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
    //            SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
    //            SimonDB.CreDbPar("@ChangeMoney", GoodsDR["exchangecoin"].ToString())
    //        });
    //        orderremark = "奖卷兑换金币,金币实时到账";
    //    }
    //    //奖卷兑换话费卡,自动发卡
    //    if (GoodsDR["type"].ToString().Equals("2"))
    //    {
    //        //ID正序发卡
    //        DataTable MobileFeeCardDT = SimonDB.DataTable(@"select top 1 * from MobileFeeCard where mobilefee=@mobilefee and isgrant=0 order by id asc", new DbParameter[] {
    //            SimonDB.CreDbPar("@mobilefee", GoodsDR["exchangemobilefee"].ToString())
    //        });
    //        if (MobileFeeCardDT.Rows.Count <= 0)
    //        {
    //            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品库存不足"));
    //        }
    //        DataRow MobileFeeCardDR = MobileFeeCardDT.Rows[0];
    //        orderremark = "话费金额:" + MobileFeeCardDR["mobilefee"].ToString() + "元"
    //                    + "充值卡号:" + MobileFeeCardDR["cardno"].ToString()
    //                    + "充值卡密:" + MobileFeeCardDR["cardpwd"].ToString()
    //                    + "充值网址:" + MobileFeeCardDR["rechargeurl"].ToString();
    //        SimonDB.ExecuteNonQuery(@"update MobileFeeCard set isgrant=1,granttouserid=@granttouserid,grantdate=getdate() where id=@id", new DbParameter[] {
    //            SimonDB.CreDbPar("@granttouserid", userid),
    //            SimonDB.CreDbPar("@id", MobileFeeCardDR["id"].ToString())
    //        });
    //    }
    //    //附赠金币项
    //    if (GoodsDR["givecoin"].ToString() != "0")
    //    {
    //        //增加金币(附赠)
    //        SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@changemoney where userid=@userid", new DbParameter[] {
    //            SimonDB.CreDbPar("@changemoney", long.Parse(GoodsDR["givecoin"].ToString())),
    //            SimonDB.CreDbPar("@userid", userid)
    //        });
    //        //写入金币变化日志(附赠)
    //        SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
    //                                                          values (@UserID,@UserName,@StartMoney,@ChangeMoney,10,getdate(),'奖卷兑换金币,金币实时到账')", new DbParameter[] {
    //            SimonDB.CreDbPar("@UserID", userid),
    //            SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
    //            SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
    //            SimonDB.CreDbPar("@ChangeMoney", GoodsDR["givecoin"].ToString())
    //        });
    //    }

    //    //奖卷兑换订单
    //    List<DbParameter> order_lpar = new List<DbParameter>();
    //    order_lpar.Add(SimonDB.CreDbPar("@userid", userid));
    //    order_lpar.Add(SimonDB.CreDbPar("@goodsid", goodsid));
    //    order_lpar.Add(SimonDB.CreDbPar("@goodstype", GoodsDR["type"].ToString()));
    //    order_lpar.Add(SimonDB.CreDbPar("@goodstitle", GoodsDR["title"].ToString()));
    //    order_lpar.Add(SimonDB.CreDbPar("@prizeprice", GoodsDR["prizeprice"].ToString()));
    //    order_lpar.Add(SimonDB.CreDbPar("@exchangecoin", GoodsDR["type"].ToString().Equals("1") ? GoodsDR["exchangecoin"].ToString() : "0")); //金币商品
    //    order_lpar.Add(SimonDB.CreDbPar("@exchangemobilefee", GoodsDR["type"].ToString().Equals("2") ? GoodsDR["exchangemobilefee"].ToString() : "0")); //话费商品
    //    order_lpar.Add(SimonDB.CreDbPar("@givecoin", GoodsDR["givecoin"].ToString()));
    //    order_lpar.Add(SimonDB.CreDbPar("@realname", realname != null ? realname : string.Empty));
    //    order_lpar.Add(SimonDB.CreDbPar("@mobile", mobile != null ? mobile : string.Empty));
    //    order_lpar.Add(SimonDB.CreDbPar("@address", address != null ? address : string.Empty));
    //    order_lpar.Add(SimonDB.CreDbPar("@orderremark", orderremark)); //金币商品,写入备注
    //    order_lpar.Add(SimonDB.CreDbPar("@orderstate", "0"));  //订单初始状态
    //    order_lpar.Add(SimonDB.CreDbPar("@osdate0", DateTime.Now.ToString())); //订单初始状态更新时间

    //    int orderid = SimonDB.Insert(@"insert into ExchangeOrder (userid,goodsid,goodstype,goodstitle,prizeprice,exchangecoin,exchangemobilefee,
    //                                                              givecoin,realname,mobile,address,orderremark,orderstate,osdate0) 
    //                                                      values (@userid,@goodsid,@goodstype,@goodstitle,@prizeprice,@exchangecoin,@exchangemobilefee,
    //                                                              @givecoin,@realname,@mobile,@address,@orderremark,@orderstate,@osdate0)", order_lpar.ToArray());

    //    //奖卷兑换金币 或 奖卷兑换话费卡,标记订单为已处理
    //    if (GoodsDR["type"].ToString().Equals("1") || GoodsDR["type"].ToString().Equals("2"))
    //    {
    //        SimonDB.ExecuteNonQuery(@"update ExchangeOrder set orderstate=1,osdate1=getdate() where id=@id", new DbParameter[] { SimonDB.CreDbPar("@id", orderid) });
    //    }


    //    //更新商品库存
    //    int curr_inventory = result_inventory - 1; //当前库存
    //    SimonDB.ExecuteNonQuery(@"update ExchangeGoods set inventory=@inventory where id=@id", new DbParameter[] {
    //        SimonDB.CreDbPar("@inventory", curr_inventory),
    //        SimonDB.CreDbPar("@id", goodsid)
    //    });

    //    JsonData jd = new JsonData();
    //    jd["code"] = "1";
    //    jd["msg"] = "success";
    //    jd["results"] = new JsonData();
    //    jd["results"]["orderid"] = orderid.ToString();
    //    jd["results"]["curr_inventory"] = curr_inventory.ToString();

    //    SimonUtils.RespWNC(jd.ToJson());
    //}
    //#endregion

    //#region 奖卷兑换订单列表(分页)
    //protected void ExchangeOrderList()
    //{
    //    CheckSign();
    //    string pageindex = SimonUtils.Qnum("pageindex");  //页码
    //    string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
    //    string userid = SimonUtils.Qnum("userid");  //用户ID
    //    string goodstype = SimonUtils.Qnum("goodstype");  //(非必选)商品类型 1、金币；2、话费；3实物；（前端 订单列表只显示话费订单(2),实物订单(3),不显示金币订单）
    //    string state = SimonUtils.Qnum("state");  //(非必选)订单状态 0 未处理，1 已处理

    //    if (pageindex.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
    //    }
    //    if (pagesize.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
    //    }
    //    if (userid.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
    //    }

    //    List<DbParameter> lpar = new List<DbParameter>();
    //    List<string> lwhere = new List<string>();

    //    if (goodstype.Length > 0)
    //    {
    //        lpar.Add(SimonDB.CreDbPar("@goodstype", goodstype));
    //        lwhere.Add("goodstype=@goodstype");
    //    }
    //    if (state.Length > 0)
    //    {
    //        lpar.Add(SimonDB.CreDbPar("@state", state));
    //        lwhere.Add("state=@state");
    //    }

    //    lwhere.Add("(goodstype=2 or goodstype=3)"); //前端 订单列表只显示话费订单(2),实物订单(3),不显示金币订单

    //    lpar.Add(SimonDB.CreDbPar("@userid", userid)); //限定用户ID
    //    lwhere.Add("userid=@userid");

    //    string _countsql = @"select count(1) from ExchangeOrder {0}";
    //    string _listsql = @"select * from (
    //                                select row_number() over (order by id desc) as row, * from ExchangeOrder {0}
    //                            ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

    //    if (lwhere.Count > 0)
    //    {
    //        string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
    //        _countsql = string.Format(_countsql, _sqlwhere);
    //        _listsql = string.Format(_listsql, _sqlwhere);
    //    }
    //    else
    //    {
    //        _countsql = string.Format(_countsql, string.Empty);
    //        _listsql = string.Format(_listsql, string.Empty);
    //    }

    //    int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
    //    int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
    //    if (pageindex == "0") pageindex = "1";  //默认第1页
    //    if (pagesize == "0") pagesize = "10";  //默认每页10条
    //    if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

    //    lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
    //    lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

    //    DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
    //    List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
    //    foreach (DataRow DR in ListDT.Rows)
    //    {
    //        Dictionary<string, string> tempdic = new Dictionary<string, string>();
    //        tempdic.Add("id", DR["id"].ToString());
    //        tempdic.Add("userid", DR["userid"].ToString());
    //        tempdic.Add("goodsid", DR["goodsid"].ToString());
    //        tempdic.Add("goodstype", DR["goodstype"].ToString());
    //        tempdic.Add("goodstitle", DR["goodstitle"].ToString());
    //        tempdic.Add("prizeprice", DR["prizeprice"].ToString());
    //        tempdic.Add("exchangecoin", DR["exchangecoin"].ToString());
    //        tempdic.Add("exchangemobilefee", DR["exchangemobilefee"].ToString());
    //        tempdic.Add("givecoin", DR["givecoin"].ToString());
    //        tempdic.Add("realname", DR["realname"].ToString());
    //        tempdic.Add("mobile", DR["mobile"].ToString());
    //        tempdic.Add("address", DR["address"].ToString());
    //        tempdic.Add("orderremark", DR["orderremark"].ToString());
    //        tempdic.Add("orderstate", DR["orderstate"].ToString());
    //        tempdic.Add("osdate0", DateTime.Parse(DR["osdate0"].ToString()).ToString("yyyy/MM/dd"));
    //        tempdic.Add("osdate1", !string.IsNullOrWhiteSpace(DR["osdate1"].ToString()) ? DateTime.Parse(DR["osdate1"].ToString()).ToString("yyyy/MM/dd") : "");

    //        resultslist.Add(tempdic);
    //    }

    //    Dictionary<string, object> jsondic = new Dictionary<string, object>();
    //    jsondic.Add("code", "1");
    //    jsondic.Add("msg", "success");
    //    jsondic.Add("recordcount", RecordCount.ToString());
    //    jsondic.Add("totalpage", TotalPage.ToString());
    //    jsondic.Add("pagesize", pagesize);
    //    jsondic.Add("pageindex", pageindex);
    //    jsondic.Add("results", resultslist);

    //    SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    //}
    //#endregion

    //#region 奖卷兑换订单广播
    //protected void ExchangeOrderNotice()
    //{
    //    CheckSign();
    //    string topcount = SimonUtils.Qnum("topcount");  //获取记录数量

    //    if (topcount.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "获取记录数量错误"));
    //    }

    //    string _listsql = string.Format(@"select top {0} * from ExchangeOrder as a inner join TUsers as b on a.userid=b.userid where orderstate=1 order by id desc", topcount);
    //    DataTable ListDT = SimonDB.DataTable(_listsql);

    //    List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
    //    foreach (DataRow DR in ListDT.Rows)
    //    {
    //        Dictionary<string, string> tempdic = new Dictionary<string, string>();
    //        tempdic.Add("id", DR["id"].ToString());
    //        tempdic.Add("nickname", DR["NickName"].ToString());
    //        tempdic.Add("goodstitle", DR["goodstitle"].ToString());
    //        tempdic.Add("prizeprice", DR["prizeprice"].ToString());
    //        tempdic.Add("osdate1", DateTime.Parse(DR["osdate1"].ToString()).ToString("yyyy年MM月dd日HH时mm分"));

    //        resultslist.Add(tempdic);
    //    }

    //    Dictionary<string, object> jsondic = new Dictionary<string, object>();
    //    jsondic.Add("code", "1");
    //    jsondic.Add("msg", "success");
    //    jsondic.Add("results", resultslist);

    //    SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    //}
    //#endregion

    //#region 奖卷兑换订单详情
    //protected void ExchangeOrderDetails()
    //{
    //    CheckSign();
    //    string userid = SimonUtils.Qnum("userid");  //用户ID 
    //    string id = SimonUtils.Qnum("id");  //订单ID

    //    if (userid.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
    //    }
    //    if (id.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单ID错误(数字类型)"));
    //    }

    //    DataTable DT = SimonDB.DataTable(@"select * from ExchangeOrder where userid=@userid and id=@id", new DbParameter[] {
    //        SimonDB.CreDbPar("@userid", userid),
    //        SimonDB.CreDbPar("@id", id)
    //    });
    //    if (DT.Rows.Count <= 0)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单不存在"));
    //    }

    //    DataRow DR = DT.Rows[0];

    //    JsonData jd = new JsonData();
    //    jd["code"] = "1";
    //    jd["msg"] = "success";
    //    jd["results"] = new JsonData();
    //    jd["results"]["id"] = DR["id"].ToString();
    //    jd["results"]["userid"] = DR["userid"].ToString();
    //    jd["results"]["goodsid"] = DR["goodsid"].ToString();
    //    jd["results"]["goodstype"] = DR["goodstype"].ToString();
    //    jd["results"]["goodstitle"] = DR["goodstitle"].ToString();
    //    jd["results"]["prizeprice"] = DR["prizeprice"].ToString();
    //    jd["results"]["exchangecoin"] = DR["exchangecoin"].ToString();
    //    jd["results"]["exchangemobilefee"] = DR["exchangemobilefee"].ToString();
    //    jd["results"]["givecoin"] = DR["givecoin"].ToString();
    //    jd["results"]["realname"] = DR["realname"].ToString();
    //    jd["results"]["mobile"] = DR["mobile"].ToString();
    //    jd["results"]["address"] = DR["address"].ToString();
    //    jd["results"]["orderremark"] = DR["orderremark"].ToString();
    //    jd["results"]["orderstate"] = DR["orderstate"].ToString();
    //    jd["results"]["osdate0"] = DR["osdate0"].ToString();
    //    jd["results"]["osdate1"] = DR["osdate1"].ToString();

    //    SimonUtils.RespWNC(jd.ToJson());
    //}
    //#endregion

    #region 兑换奖品列表(分页)
    protected void ExchangeGoodsList()
    {
        CheckSign();
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string type = SimonUtils.Qnum("type");  //(非必选)商品支持兑换 1、金币；2、奖券；

        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();

        if (type.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@type", type));
            lwhere.Add("type=@type");
        }

        string _countsql = @"select count(1) from ExchangeGoods {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by sort asc, id desc) as row, * from ExchangeGoods {0}
                                ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("id", DR["id"].ToString());  //商品id
            tempdic.Add("type", DR["type"].ToString());  //商品兑换货币类型 1、金币；2、奖券；
            tempdic.Add("title", DR["title"].ToString());  //商品标题
            tempdic.Add("img", DR["img"].ToString());  //商品标题图片url
            tempdic.Add("des", DR["des"].ToString());  //商品描述
            tempdic.Add("inventory", DR["inventory"].ToString());  //商品库存
            tempdic.Add("goldprice", DR["goldprice"].ToString());  //商品金币兑换价格
            tempdic.Add("lotteriesprice", DR["lotteriesprice"].ToString());  //商品奖卷兑换价格
            tempdic.Add("prizeprice", DR["prizeprice"].ToString());  //奖品价值
            tempdic.Add("givecoin", DR["givecoin"].ToString());  //附赠金币额
            tempdic.Add("sort", DR["sort"].ToString());  //排序
            tempdic.Add("updatetime", DR["updatetime"].ToString());  //更新时间

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 兑换奖品详情
    protected void ExchangeGoodsDetails()
    {
        CheckSign();
        string id = SimonUtils.Qnum("id");  //商品ID

        if (id.Length < 1 || !SimonUtils.IsNum(id))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品ID错误"));
        }

        DataTable DT = SimonDB.DataTable(@"select * from ExchangeGoods where id=@id", new DbParameter[] { SimonDB.CreDbPar("@id", id) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品不存在"));
        }

        DataRow DR = DT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["id"] = DR["id"].ToString();
        jd["results"]["type"] = DR["type"].ToString();
        jd["results"]["title"] = DR["title"].ToString();
        jd["results"]["img"] = DR["img"].ToString();
        jd["results"]["des"] = DR["des"].ToString();
        jd["results"]["inventory"] = DR["inventory"].ToString();
        jd["results"]["prizeprice"] = DR["prizeprice"].ToString();
        jd["results"]["goldprice"] = DR["goldprice"].ToString();
        jd["results"]["lotteriesprice"] = DR["lotteriesprice"].ToString();
        jd["results"]["givecoin"] = DR["givecoin"].ToString();
        jd["results"]["sort"] = DR["sort"].ToString();
        jd["results"]["updatetime"] = DR["updatetime"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 兑换奖品
    protected void ExchangeOrder()
    {
        CheckSign();
        string type = SimonUtils.Qnum("type"); //兑换货币类型 1、金币 ；2、奖券；
        string userid = SimonUtils.Qnum("userid"); //用户ID
        string goodsid = SimonUtils.Qnum("goodsid"); //商品ID
        string realname = Request.Params["realname"]; //姓名1-200个字符(必填项)
        string mobile = Request.Params["mobile"]; //手机号1-50个字符(必填项)
        string remarks = Request.Params["remarks"]; //备注

        if (type.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "兑换类型错误(数字类型)"));
        }
        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
        }
        if (goodsid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品ID错误(数字类型)"));
        }
        if (string.IsNullOrWhiteSpace(realname) || realname.Length > 200)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "姓名1-200个字符不可为空"));
        }
        if (string.IsNullOrWhiteSpace(mobile) || mobile.Length > 50)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "手机号1-50个字符，不可为空"));
        }
        if (string.IsNullOrWhiteSpace(remarks))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "备注不可为空"));
        }

        //读取商品信息
        DataTable GoodsDT = SimonDB.DataTable(@"select * from ExchangeGoods where id=@id", new DbParameter[] { SimonDB.CreDbPar("@id", goodsid) });
        if (GoodsDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品不存在"));
        }
        DataRow GoodsDR = GoodsDT.Rows[0];
        //库存数值
        int result_inventory = int.Parse(GoodsDR["inventory"].ToString());

        //检查库存
        if (result_inventory <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品库存不足"));
        }

        //读取用户信息
        DbParameter[] user_parms = new DbParameter[] { SimonDB.CreDbPar("@userid", userid) };
        DataTable UserDT = SimonDB.DataTable(@"select a.*, b.WalletMoney, c.Lotteries 
                                               from TUsers as a 
                                               inner join TUserInfo as b on a.userid=b.userid
                                               inner join Web_Users as c on a.userid=c.userid
                                               where a.userid=@userid", user_parms);
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];
        if (type == "1" && (long.Parse(GoodsDR["goldprice"].ToString()) > long.Parse(UserDR["WalletMoney"].ToString())))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您的钱包金币不足,无法兑换该商品"));
        }
        if (type == "2" && (long.Parse(GoodsDR["lotteriesprice"].ToString()) > long.Parse(UserDR["Lotteries"].ToString())))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您的奖卷不足,无法兑换该商品"));
        }

        //判断用户是否在游戏中
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", user_parms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", user_parms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}
        if (type == "1")
        {
            //扣除金币
            SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney-@changemoney where userid=@userid", new DbParameter[] {
                SimonDB.CreDbPar("@changemoney", long.Parse(GoodsDR["goldprice"].ToString())),
                SimonDB.CreDbPar("@userid", userid)
            });
            //写入金币变化日志
            SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,OpuserType,DateTime,Remark)
                                                              values (@UserID,@UserName,@StartMoney,0-@ChangeMoney,10,1,getdate(),'金币兑换奖品_【商城】')", new DbParameter[] {
                SimonDB.CreDbPar("@UserID", userid),
                SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
                SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
                SimonDB.CreDbPar("@ChangeMoney", GoodsDR["goldprice"].ToString())
            });
        }
        if (type == "2")
        {
            //扣除奖卷
            SimonDB.ExecuteNonQuery(@"update Web_Users set Lotteries=(Lotteries-@ChangeLotteries) where UserID=@UserID", new DbParameter[] {
            SimonDB.CreDbPar("@UserID", userid),
            SimonDB.CreDbPar("@ChangeLotteries", GoodsDR["lotteriesprice"].ToString())
            });
            //写入奖卷变化日志
            SimonDB.ExecuteNonQuery(@"insert into LogLotteries (UserId,PreLotteries,ChangeLotteries,CurLotteries,CollectDate)
	                                                values (@UserId,@PreLotteries,@ChangeLotteries,@CurLotteries,GETDATE())", new DbParameter[] {
            SimonDB.CreDbPar("@UserId", userid),
            SimonDB.CreDbPar("@PreLotteries", UserDR["Lotteries"].ToString()),
            SimonDB.CreDbPar("@ChangeLotteries", GoodsDR["lotteriesprice"].ToString()),
            SimonDB.CreDbPar("@CurLotteries", long.Parse(UserDR["Lotteries"].ToString()) - long.Parse(GoodsDR["lotteriesprice"].ToString()))
            });
        }
        //附赠金币项
        if (GoodsDR["givecoin"].ToString() != "0")
        {
            //增加金币(附赠)
            SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@changemoney where userid=@userid", new DbParameter[] {
                SimonDB.CreDbPar("@changemoney", long.Parse(GoodsDR["givecoin"].ToString())),
                SimonDB.CreDbPar("@userid", userid)
            });
            //写入金币变化日志(附赠)
            SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,OpuserType,DateTime,Remark)
                                                              values (@UserID,@UserName,@StartMoney,@ChangeMoney,10,1,getdate(),'商城兑换奖品附赠金币')", new DbParameter[] {
                SimonDB.CreDbPar("@UserID", userid),
                SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
                SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
                SimonDB.CreDbPar("@ChangeMoney", GoodsDR["givecoin"].ToString())
            });
        }

        //订单备注信息(不同类型商品标记备注信息不同)
        string orderremark = string.Empty;

        //奖卷兑换订单
        List<DbParameter> order_lpar = new List<DbParameter>();
        order_lpar.Add(SimonDB.CreDbPar("@userid", userid));
        order_lpar.Add(SimonDB.CreDbPar("@goodsid", goodsid));
        order_lpar.Add(SimonDB.CreDbPar("@goodstype", GoodsDR["type"].ToString()));
        order_lpar.Add(SimonDB.CreDbPar("@goodstitle", GoodsDR["title"].ToString()));
        order_lpar.Add(SimonDB.CreDbPar("@prizeprice", GoodsDR["prizeprice"].ToString()));
        order_lpar.Add(SimonDB.CreDbPar("@goldprice", type.Equals("1") ? GoodsDR["goldprice"].ToString() : "0")); //金币商品
        order_lpar.Add(SimonDB.CreDbPar("@lotteriesprice", type.Equals("2") ? GoodsDR["lotteriesprice"].ToString() : "0")); //奖券商品
        order_lpar.Add(SimonDB.CreDbPar("@givecoin", GoodsDR["givecoin"].ToString()));
        order_lpar.Add(SimonDB.CreDbPar("@realname", realname != null ? realname : string.Empty));
        order_lpar.Add(SimonDB.CreDbPar("@mobile", mobile != null ? mobile : string.Empty));
        order_lpar.Add(SimonDB.CreDbPar("@orderremark", remarks != null ? remarks : string.Empty));
        order_lpar.Add(SimonDB.CreDbPar("@orderstate", "0"));  //订单初始状态
        order_lpar.Add(SimonDB.CreDbPar("@osdate0", DateTime.Now.ToString())); //订单初始状态更新时间

        int orderid = SimonDB.Insert(@"insert into ExchangeOrder (userid,goodsid,goodstype,goodstitle,prizeprice,goldprice,lotteriesprice,
                                                                  givecoin,realname,mobile,orderremark,orderstate,osdate0) 
                                                          values (@userid,@goodsid,@goodstype,@goodstitle,@prizeprice,@goldprice,@lotteriesprice,
                                                                  @givecoin,@realname,@mobile,@orderremark,@orderstate,@osdate0)", order_lpar.ToArray());

        //更新商品库存
        int curr_inventory = result_inventory - 1; //当前库存
        SimonDB.ExecuteNonQuery(@"update ExchangeGoods set inventory=@inventory where id=@id", new DbParameter[] {
            SimonDB.CreDbPar("@inventory", curr_inventory),
            SimonDB.CreDbPar("@id", goodsid)
        });

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["orderid"] = orderid.ToString();
        jd["results"]["curr_inventory"] = curr_inventory.ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 兑换奖品订单列表(分页)
    protected void ExchangeOrderList()
    {
        CheckSign();
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string userid = SimonUtils.Qnum("userid");  //用户ID

        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }
        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();

        lpar.Add(SimonDB.CreDbPar("@userid", userid)); //限定用户ID
        lwhere.Add("userid=@userid");

        string _countsql = @"select count(1) from ExchangeOrder {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by id desc) as row, * from ExchangeOrder {0}
                                ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("id", DR["id"].ToString());
            tempdic.Add("userid", DR["userid"].ToString());
            tempdic.Add("goodsid", DR["goodsid"].ToString());
            tempdic.Add("goodstype", DR["goodstype"].ToString());//1、金币；2、奖券；
            tempdic.Add("goodstitle", DR["goodstitle"].ToString());
            tempdic.Add("prizeprice", DR["prizeprice"].ToString());
            tempdic.Add("goldprice", DR["goldprice"].ToString());
            tempdic.Add("lotteriesprice", DR["lotteriesprice"].ToString());
            tempdic.Add("givecoin", DR["givecoin"].ToString());
            tempdic.Add("realname", DR["realname"].ToString());
            tempdic.Add("mobile", DR["mobile"].ToString());
            tempdic.Add("address", DR["address"].ToString());
            tempdic.Add("orderremark", DR["orderremark"].ToString());
            tempdic.Add("orderstate", DR["orderstate"].ToString());
            tempdic.Add("osdate0", DateTime.Parse(DR["osdate0"].ToString()).ToString("yyyy/MM/dd"));
            tempdic.Add("osdate1", !string.IsNullOrWhiteSpace(DR["osdate1"].ToString()) ? DateTime.Parse(DR["osdate1"].ToString()).ToString("yyyy/MM/dd") : "");

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 兑换奖品广播
    protected void ExchangeOrderNotice()
    {
        CheckSign();
        string topcount = SimonUtils.Qnum("topcount");  //获取记录数量

        if (topcount.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "获取记录数量错误"));
        }

        string _listsql = string.Format(@"select top {0} * from ExchangeOrder as a inner join TUsers as b on a.userid=b.userid where orderstate=1 order by id desc", topcount);
        DataTable ListDT = SimonDB.DataTable(_listsql);

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("id", DR["id"].ToString());
            tempdic.Add("nickname", DR["NickName"].ToString());
            tempdic.Add("goodstitle", DR["goodstitle"].ToString());
            tempdic.Add("prizeprice", DR["prizeprice"].ToString());
            tempdic.Add("osdate1", DateTime.Parse(DR["osdate1"].ToString()).ToString("yyyy年MM月dd日HH时mm分"));

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 兑换奖品详情
    protected void ExchangeOrderDetails()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid");  //用户ID 
        string id = SimonUtils.Qnum("id");  //订单ID

        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
        }
        if (id.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单ID错误(数字类型)"));
        }

        DataTable DT = SimonDB.DataTable(@"select * from ExchangeOrder where userid=@userid and id=@id", new DbParameter[] {
            SimonDB.CreDbPar("@userid", userid),
            SimonDB.CreDbPar("@id", id)
        });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单不存在"));
        }

        DataRow DR = DT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["id"] = DR["id"].ToString();
        jd["results"]["userid"] = DR["userid"].ToString();
        jd["results"]["goodsid"] = DR["goodsid"].ToString();
        jd["results"]["goodstype"] = DR["goodstype"].ToString();
        jd["results"]["goodstitle"] = DR["goodstitle"].ToString();
        jd["results"]["prizeprice"] = DR["prizeprice"].ToString();
        jd["results"]["goldprice"] = DR["goldprice"].ToString();
        jd["results"]["lotteriesprice"] = DR["lotteriesprice"].ToString();
        jd["results"]["givecoin"] = DR["givecoin"].ToString();
        jd["results"]["realname"] = DR["realname"].ToString();
        jd["results"]["mobile"] = DR["mobile"].ToString();
        jd["results"]["address"] = DR["address"].ToString();
        jd["results"]["orderremark"] = DR["orderremark"].ToString();
        jd["results"]["orderstate"] = DR["orderstate"].ToString();
        jd["results"]["osdate0"] = DR["osdate0"].ToString();
        jd["results"]["osdate1"] = DR["osdate1"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 保险箱赠送、接收记录
    protected void MoneyTransLog()
    {
        CheckSign();
        string topcount = SimonUtils.Qnum("topcount");  //获取记录数量
        string userid = SimonUtils.Qnum("userid");  //用户ID 
        string transtype = SimonUtils.Qnum("transtype");  //记录类型： 1赠送，2接收

        if (topcount.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "获取记录数量错误"));
        }
        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
        }
        if (transtype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "记录类型错误(数字类型)"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers where userid=@userid", lpar.ToArray());
        if (UserDT.Rows.Count <= 0)
        {
            return;
        }

        lpar.Add(SimonDB.CreDbPar("@username", UserDT.Rows[0]["username"].ToString()));

        string _listsql = "";
        if (transtype == "0")
        {
            _listsql = string.Format(@"select top {0} * from Web_TransLog where {1} order by id desc", topcount, "username=@username");
        }
        else if (transtype == "1")
        {
            _listsql = string.Format(@"select top {0} * from Web_TransLog where {1} order by id desc", topcount, "usernamezz=@username");
        }
        if (string.IsNullOrWhiteSpace(_listsql))
        {
            return;
        }

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            DataTable _tmp_userdt = SimonDB.DataTable(@"select * from TUsers where username=@username", new DbParameter[] {
                SimonDB.CreDbPar("@username", DR["UserName"].ToString())
            });
            DataTable _tmp_userdt_zz = SimonDB.DataTable(@"select * from TUsers where username=@username", new DbParameter[] {
                SimonDB.CreDbPar("@username", DR["UserNameZZ"].ToString())
            });

            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("userid", _tmp_userdt.Rows.Count > 0 ? _tmp_userdt.Rows[0]["UserID"].ToString() : "");
            tempdic.Add("nickname", _tmp_userdt.Rows.Count > 0 ? _tmp_userdt.Rows[0]["NickName"].ToString() : "");
            tempdic.Add("userid_zz", _tmp_userdt_zz.Rows.Count > 0 ? _tmp_userdt_zz.Rows[0]["UserID"].ToString() : "");
            tempdic.Add("nickname_zz", _tmp_userdt_zz.Rows.Count > 0 ? _tmp_userdt_zz.Rows[0]["NickName"].ToString() : "");
            tempdic.Add("money", DR["Money"].ToString());
            tempdic.Add("transtime", DR["TransTime"].ToString());
            tempdic.Add("success", DR["Success"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 苹果内购
    protected void AppStoreReceipt()
    {
        using (StreamReader receive_stream = new StreamReader(Request.InputStream))
        {
            string receive_str = receive_stream.ReadToEnd();
            NameValueCollection receive_nvc = SimonUrl.GetNVC(receive_str);
            string t = receive_nvc["t"];  //unix时间戳 (10位数字)
            string sign = receive_nvc["sign"];  //签名
            string userid = receive_nvc["userid"];  //用户ID
            string payip = receive_nvc["payip"];  //用户IP
            string transaction_id = receive_nvc["transaction_id"];  //transaction_id
            string posturl = receive_nvc["posturl"];  //posturl
            string receipt_data = receive_nvc["receipt_data"];  //receipt_data
            //SimonUtils.RespWNC(receipt_data);

            //JsonData receive_jd = null;
            //try
            //{
            //    receive_jd = JsonMapper.ToObject(receive_str);
            //}
            //catch { SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "Json异常")); }

            //string t = ((IDictionary)receive_jd).Contains("t") ? (string)receive_jd["t"] : null;  //unix时间戳 (10位数字)
            //string sign = ((IDictionary)receive_jd).Contains("sign") ? (string)receive_jd["sign"] : null;  //签名
            //string userid = ((IDictionary)receive_jd).Contains("userid") ? (string)receive_jd["userid"] : null;  //用户ID
            //string payip = ((IDictionary)receive_jd).Contains("payip") ? (string)receive_jd["payip"] : null;  //用户IP
            //string transaction_id = ((IDictionary)receive_jd).Contains("transaction_id") ? (string)receive_jd["transaction_id"] : null;  //transaction_id
            //string posturl = ((IDictionary)receive_jd).Contains("posturl") ? (string)receive_jd["posturl"] : null;  //posturl
            //string receipt_data = ((IDictionary)receive_jd).Contains("receipt_data") ? (string)receive_jd["receipt_data"] : null;  //receipt_data

            if (string.IsNullOrWhiteSpace(t) || t.Length != 10)
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "时间戳错误"));
            }
            if (CurrSite.ApiCallTimeOut(t))
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请求超时"));
            }
            if (!CurrSite.VerifySign(sign, t))
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "签名错误"));
            }
            if (string.IsNullOrWhiteSpace(userid))
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误"));
            }
            if (string.IsNullOrWhiteSpace(payip))
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户IP错误"));
            }
            if (string.IsNullOrWhiteSpace(transaction_id))
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请提供 transaction_id"));
            }
            if (string.IsNullOrWhiteSpace(posturl))
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请提供 posturl"));
            }
            if (string.IsNullOrWhiteSpace(receipt_data))
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请提供 receipt_data"));
            }

            posturl = Server.UrlDecode(posturl);
            receipt_data = Server.UrlDecode(receipt_data);
            string post_iap_data = "{\"receipt-data\":\"" + receipt_data + "\"}";
            using (HttpWebResponse response_iap = SimonHttp.CreatePostHttpResponse(posturl, post_iap_data, null, "application/json", null, null, null))
            using (StreamReader response_iap_stream = new StreamReader(response_iap.GetResponseStream()))
            {
                //获取返回值
                string post_iap_result = response_iap_stream.ReadToEnd();
                //写日志
                StringBuilder sb = new StringBuilder();
                sb.Append("\r\n 测试日志 苹果内购-----------------------------------------------------------------------------------");
                sb.Append("\r\n StatusCode=" + response_iap.StatusCode.ToString());
                sb.Append("\r\n result=" + post_iap_result);
                sb.Append("\r\n--------------------------------------------------------------------------------------------------");
                SimonLog.WriteLog(sb.ToString(), "/Log/", "log_AppStoreReceipt_" + DateTime.Now.ToString("yyyyMMdd"));
                //返回数据提取
                JsonData r_jd = JsonMapper.ToObject(post_iap_result);
                string r_status = r_jd["status"].ToString();
                string r_quantity = r_jd["receipt"]["in_app"][0]["quantity"].ToString();
                string r_product_id = r_jd["receipt"]["in_app"][0]["product_id"].ToString();
                string r_product_money = r_product_id.Replace("gngame_", ""); //充值金额
                string r_product_coin = r_product_id.Replace("gngame_", ""); //充值金币数量
                string r_transaction_id = r_jd["receipt"]["in_app"][0]["transaction_id"].ToString();

                if (r_status != "0")
                {
                    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "交易失败 status=" + r_status));
                }
                if (transaction_id != r_transaction_id)
                {
                    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "transaction_id 错误"));
                }
                //判断玩家账号是否存在
                DbParameter[] userparms = new DbParameter[] { SimonDB.CreDbPar("@userid", userid) };
                DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", userparms);
                if (UserDT.Rows.Count <= 0)
                {
                    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
                }
                DataRow UserDR = UserDT.Rows[0];

                //判断用户是否在游戏中
                //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", userparms) > 0)
                //{
                //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
                //}
                //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", userparms) > 0)
                //{
                //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
                //}

                //创建订单
                List<DbParameter> rmbcost_lpar = new List<DbParameter>();
                rmbcost_lpar.Add(SimonDB.CreDbPar("@Users_ids", UserDR["UserID"].ToString()));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@TrueName", UserDR["NickName"].ToString()));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@PayMoney", r_product_money));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@PayType", "101"));  //苹果内购 支付类型设置为101
                rmbcost_lpar.Add(SimonDB.CreDbPar("@TypeInfo", "苹果内购"));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", r_transaction_id));  //订单号
                rmbcost_lpar.Add(SimonDB.CreDbPar("@AddTime", DateTime.Now.ToString()));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@ExchangeRate", "1"));  //充值兑换率(此字段暂时无效)
                rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", r_product_money));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@InSuccess", true));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@PaySuccess", true));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@MoneyFront", UserDR["WalletMoney"].ToString()));
                rmbcost_lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "1"));  //更新状态
                rmbcost_lpar.Add(SimonDB.CreDbPar("@PurchaseType", "1"));  //充值金币1 充值元宝2
                rmbcost_lpar.Add(SimonDB.CreDbPar("@PayIP", payip));

                //已充值判断
                if ((int)SimonDB.ExecuteScalar(@"select count(*) from Web_RMBCost where OrderID=@OrderID", rmbcost_lpar.ToArray()) > 0)
                {
                    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该订单已完成充值"));
                }

                SimonDB.ExecuteNonQuery(@"insert into Web_RMBCost (Users_ids,TrueName,UserName,PayMoney,PayType,TypeInfo,OrderID,AddTime,
                                                                   ExchangeRate,InMoney,InSuccess,PaySuccess,MoneyFront,UpdateFlag,PurchaseType,
                                                                   PayIP)
                                                           values (@Users_ids,@TrueName,@UserName,@PayMoney,@PayType,@TypeInfo,@OrderID,@AddTime,
                                                                   @ExchangeRate,@InMoney,@InSuccess,@PaySuccess,@MoneyFront,@UpdateFlag,@PurchaseType,
                                                                   @PayIP)", rmbcost_lpar.ToArray());

                //充值动作
                SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@ChangeMoney where UserID=@UserID", new DbParameter[] {
                    SimonDB.CreDbPar("@ChangeMoney", r_product_coin),
                    SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString())
                });

                //金币日志
                SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
                                                                  values (@UserID,@UserName,@StartMoney,@ChangeMoney,2,getdate(),@Remark)", new DbParameter[] {
                    SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
                    SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
                    SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
                    SimonDB.CreDbPar("@ChangeMoney", r_product_coin),
                    SimonDB.CreDbPar("@Remark", "苹果内购充值，订单号：" + r_transaction_id)

                });

                //输出Json
                JsonData jd = new JsonData();
                jd["code"] = "1";
                jd["msg"] = "success";
                SimonUtils.RespWNC(jd.ToJson());
            }
        }
    }
    #endregion

    #region 获取RMB购买金币价格、赠送、图片信息
    protected void GetRechargeRate()
    {
        CheckSign();

        DataTable ListDT = SimonDB.DataTable(@"select * from RechargeRate order by RechargeRMB asc");

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            //tempdic.Add("id", DR["id"].ToString());
            tempdic.Add("rechargermb", DR["RechargeRMB"].ToString());
            tempdic.Add("rechargegold", DR["RechargeGold"].ToString());
            tempdic.Add("regivegold", DR["RegiveGold"].ToString());
            tempdic.Add("iconurl", DR["IconUrl"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 支付宝(生成订单)
    protected void AlipayGenOrder()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid");  //用户ID 
        string rechargermb = SimonUtils.Qnum("rechargermb"); //充值金额(人民币)  
        string payip = Request.Params["payip"];  //用户IP

        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
        }
        if (rechargermb.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值金额(人民币)错误(数字类型)"));
        }
        if (string.IsNullOrWhiteSpace(payip))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户IP错误"));
        }

        //判断玩家账号是否存在
        DbParameter[] userparms = new DbParameter[] { SimonDB.CreDbPar("@userid", userid) };
        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", userparms);
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];

        //判断用户是否在游戏中
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", userparms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", userparms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}

        //判断充值兑换率
        DataTable RechargeRateDT = SimonDB.DataTable(@"select * from RechargeRate where RechargeRMB=@RechargeRMB", new DbParameter[] {
            SimonDB.CreDbPar("@RechargeRMB", rechargermb)
        });
        if (RechargeRateDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "此充值金额的金币兑换率不存在"));
        }
        DataRow RechargeRateDR = RechargeRateDT.Rows[0];

        //创建订单
        string _orderdes = "充值金额:" + RechargeRateDR["RechargeRMB"].ToString() + " 兑换金币:" + RechargeRateDR["RechargeGold"].ToString() + " 赠送金币:" + RechargeRateDR["RegiveGold"].ToString();
        string _ordernum = CurrSite.GenNewOrderNum();
        while (((int)SimonDB.ExecuteScalar(@"select count(*) from Web_RMBCost where OrderID=@ordernum", new DbParameter[] {
            SimonDB.CreDbPar("@ordernum", _ordernum)
        })) > 0)
        {
            _ordernum = CurrSite.GenNewOrderNum();
        }

        //测试使用
        //rechargermb = "0.01"; //测试期间，所有商品写入0.01元测试

        List<DbParameter> rmbcost_lpar = new List<DbParameter>();
        rmbcost_lpar.Add(SimonDB.CreDbPar("@Users_ids", UserDR["UserID"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@TrueName", UserDR["NickName"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()));
        //rmbcost_lpar.Add(SimonDB.CreDbPar("@PayMoney", rechargermb == "0.01" ? "6" : rechargermb));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayMoney", rechargermb));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayType", "101"));  //支付宝支付 支付类型设置为101
        rmbcost_lpar.Add(SimonDB.CreDbPar("@TypeInfo", "支付宝支付"));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", _ordernum));  //订单号
        rmbcost_lpar.Add(SimonDB.CreDbPar("@AddTime", DateTime.Now.ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@ExchangeRate", "1"));  //充值兑换率(此字段暂时无效)
        rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", "0"));  //提交订单时写入0,确定充值成功后需更新该字段
        rmbcost_lpar.Add(SimonDB.CreDbPar("@InSuccess", false));  //In状态
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PaySuccess", false));  //Pay状态
        rmbcost_lpar.Add(SimonDB.CreDbPar("@MoneyFront", UserDR["WalletMoney"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "0"));  //更新状态 0 未更新  1 已更新
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PurchaseType", "1"));  //充值金币1 充值元宝2
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayIP", payip));

        SimonDB.ExecuteNonQuery(@"insert into Web_RMBCost (Users_ids,TrueName,UserName,PayMoney,PayType,TypeInfo,OrderID,AddTime,
                                                                   ExchangeRate,InMoney,InSuccess,PaySuccess,MoneyFront,UpdateFlag,PurchaseType,
                                                                   PayIP)
                                                           values (@Users_ids,@TrueName,@UserName,@PayMoney,@PayType,@TypeInfo,@OrderID,@AddTime,
                                                                   @ExchangeRate,@InMoney,@InSuccess,@PaySuccess,@MoneyFront,@UpdateFlag,@PurchaseType,
                                                                   @PayIP)", rmbcost_lpar.ToArray());


        //对接支付宝
        IAopClient client = new DefaultAopClient("https://openapi.alipay.com/gateway.do", CurrSite.Alipay_appid, CurrSite.Alipay_app_private_key, "json", "1.0", "RSA2", CurrSite.Alipay_public_key, "utf-8", false);
        //实例化具体API对应的request类,类名称和接口名称对应,当前调用接口名称如：alipay.trade.app.pay
        AlipayTradeAppPayRequest request = new AlipayTradeAppPayRequest();
        //SDK已经封装掉了公共参数，这里只需要传入业务参数。以下方法为sdk的model入参方式(model和biz_content同时存在的情况下取biz_content)。
        AlipayTradeAppPayModel model = new AlipayTradeAppPayModel();
        model.Body = _orderdes;
        model.Subject = _orderdes;
        model.TotalAmount = rechargermb;
        model.ProductCode = "QUICK_MSECURITY_PAY";
        model.OutTradeNo = _ordernum;
        model.TimeoutExpress = "30m"; //超时关闭该订单时间
        request.SetBizModel(model);
        request.SetNotifyUrl(CurrSite.Alipay_notify_url);

        //这里和普通的接口调用不同，使用的是sdkExecute
        AlipayTradeAppPayResponse response = client.SdkExecute(request);
        //HttpUtility.HtmlEncode是为了输出到页面时防止被浏览器将关键参数html转义，实际打印到日志以及http传输不会有这个问题
        //Response.Write(HttpUtility.HtmlEncode(response.Body));
        //页面输出的response.Body就是orderString 可以直接给客户端请求，无需再做处理。

        //输出Json
        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["orderid"] = _ordernum;
        jd["results"]["orderstr"] = response.Body;
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 微信支付(生成订单)
    protected void WeixinPayGenOrder()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid");  //用户ID 
        string rechargermb = SimonUtils.Qnum("rechargermb"); //充值金额(人民币)  
        string payip = Request.Params["payip"];  //用户IP

        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
        }
        if (rechargermb.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值金额(人民币)错误(数字类型)"));
        }
        if (string.IsNullOrWhiteSpace(payip))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户IP错误"));
        }

        //判断玩家账号是否存在
        DbParameter[] userparms = new DbParameter[] { SimonDB.CreDbPar("@userid", userid) };
        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", userparms);
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];

        //判断用户是否在游戏中
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", userparms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}
        //if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", userparms) > 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        //}

        //判断充值兑换率
        DataTable RechargeRateDT = SimonDB.DataTable(@"select * from RechargeRate where RechargeRMB=@RechargeRMB", new DbParameter[] {
            SimonDB.CreDbPar("@RechargeRMB", rechargermb)
        });
        if (RechargeRateDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "此充值金额的金币兑换率不存在"));
        }
        DataRow RechargeRateDR = RechargeRateDT.Rows[0];

        //创建订单
        string _orderdes = "充值金额:" + RechargeRateDR["RechargeRMB"].ToString() + " 兑换金币:" + RechargeRateDR["RechargeGold"].ToString() + " 赠送金币:" + RechargeRateDR["RegiveGold"].ToString();
        string _ordernum = CurrSite.GenNewOrderNum();
        while (((int)SimonDB.ExecuteScalar(@"select count(*) from Web_RMBCost where OrderID=@ordernum", new DbParameter[] {
            SimonDB.CreDbPar("@ordernum", _ordernum)
        })) > 0)
        {
            _ordernum = CurrSite.GenNewOrderNum();
        }

        List<DbParameter> rmbcost_lpar = new List<DbParameter>();
        rmbcost_lpar.Add(SimonDB.CreDbPar("@Users_ids", UserDR["UserID"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@TrueName", UserDR["NickName"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()));
        //rmbcost_lpar.Add(SimonDB.CreDbPar("@PayMoney", rechargermb == "0.01" ? "6" : rechargermb));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayMoney", rechargermb));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayType", "102"));  //微信支付 支付类型设置为102
        rmbcost_lpar.Add(SimonDB.CreDbPar("@TypeInfo", "微信支付"));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", _ordernum));  //订单号
        rmbcost_lpar.Add(SimonDB.CreDbPar("@AddTime", DateTime.Now.ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@ExchangeRate", "1"));  //充值兑换率(此字段暂时无效)
        rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", "0"));  //提交订单时写入0,确定充值成功后需更新该字段
        rmbcost_lpar.Add(SimonDB.CreDbPar("@InSuccess", false));  //In状态
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PaySuccess", false));  //Pay状态
        rmbcost_lpar.Add(SimonDB.CreDbPar("@MoneyFront", UserDR["WalletMoney"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "0"));  //更新状态 0 未更新  1 已更新
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PurchaseType", "1"));  //充值金币1 充值元宝2
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayIP", payip));

        SimonDB.ExecuteNonQuery(@"insert into Web_RMBCost (Users_ids,TrueName,UserName,PayMoney,PayType,TypeInfo,OrderID,AddTime,
                                                                   ExchangeRate,InMoney,InSuccess,PaySuccess,MoneyFront,UpdateFlag,PurchaseType,
                                                                   PayIP)
                                                           values (@Users_ids,@TrueName,@UserName,@PayMoney,@PayType,@TypeInfo,@OrderID,@AddTime,
                                                                   @ExchangeRate,@InMoney,@InSuccess,@PaySuccess,@MoneyFront,@UpdateFlag,@PurchaseType,
                                                                   @PayIP)", rmbcost_lpar.ToArray());

        //对接微信支付
        var timeStamp = TenPayV3Util.GetTimestamp();
        var nonceStr = TenPayV3Util.GetNoncestr();
        var rcprice = Convert.ToInt32(decimal.Parse(rechargermb)) * 100;
        var xmlDataInfo = new TenPayV3UnifiedorderRequestData(TenPayV3Info.AppId, TenPayV3Info.MchId, _orderdes, _ordernum, rcprice, payip, TenPayV3Info.TenPayV3Notify, TenPayV3Type.APP, null, TenPayV3Info.Key, nonceStr);
        var result = TenPayV3.Unifiedorder(xmlDataInfo);//调用统一订单接口
        //var package = string.Format("prepay_id={0}", result.prepay_id);
        var package = "Sign=WXPay"; //暂填写固定值Sign=WXPay

        if (!result.IsReturnCodeSuccess() || !result.IsResultCodeSuccess())
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "return_code=" + result.return_code + ";return_msg=" + result.return_msg + ";result_code=" + result.result_code));
        }

        //设置支付参数
        RequestHandler paySignReqHandler = new RequestHandler(null);
        paySignReqHandler.SetParameter("appid", TenPayV3Info.AppId);
        paySignReqHandler.SetParameter("partnerid", TenPayV3Info.MchId);
        paySignReqHandler.SetParameter("prepayid", result.prepay_id);
        paySignReqHandler.SetParameter("package", package);
        paySignReqHandler.SetParameter("noncestr", nonceStr);
        paySignReqHandler.SetParameter("timestamp", timeStamp);
        paySignReqHandler.SetParameter("signType", "MD5");
        string paySign = paySignReqHandler.CreateMd5Sign("key", TenPayV3Info.Key);

        //输出Json
        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["orderid"] = _ordernum;
        jd["results"]["appid"] = TenPayV3Info.AppId;
        jd["results"]["partnerid"] = TenPayV3Info.MchId;
        jd["results"]["prepayid"] = result.prepay_id;
        jd["results"]["package"] = package;
        jd["results"]["noncestr"] = nonceStr;
        jd["results"]["timestamp"] = timeStamp;
        jd["results"]["sign"] = paySign;
        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    #region 充值订单详情
    protected void RMBCostOrderDetails()
    {
        CheckSign();
        string orderid = Request.Params["orderid"];  //订单号

        if (string.IsNullOrWhiteSpace(orderid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单号错误"));
        }

        DataTable RMBCostDT = SimonDB.DataTable(@"select * from Web_RMBCost where OrderID=@OrderID", new DbParameter[] {
            SimonDB.CreDbPar("@OrderID", orderid)
        });
        if (RMBCostDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单不存在"));
        }
        DataRow RMBCostDR = RMBCostDT.Rows[0];

        string orderstate = "0";
        if (Convert.ToBoolean(RMBCostDR["InSuccess"]) && Convert.ToBoolean(RMBCostDR["PaySuccess"]) && RMBCostDR["UpdateFlag"].ToString() == "1")
        {
            orderstate = "1";
        }

        //输出Json
        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["orderid"] = RMBCostDR["OrderID"].ToString();
        jd["results"]["orderstate"] = orderstate;
        jd["results"]["userid"] = RMBCostDR["Users_ids"].ToString();
        jd["results"]["username"] = RMBCostDR["UserName"].ToString();
        jd["results"]["paymoney"] = RMBCostDR["paymoney"].ToString();
        jd["results"]["typeinfo"] = RMBCostDR["TypeInfo"].ToString();
        jd["results"]["addtime"] = RMBCostDR["AddTime"].ToString();
        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    #region 轮盘奖项列表
    protected void GetLotteryAwardList()
    {
        CheckSign();
        DataTable ListDT = SimonDB.DataTable(@"select * from Web_LotteryAward order by awardSort");
        int RecordCount = ListDT.Rows.Count;
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("awardid", DR["awardid"].ToString());  //奖品id
            tempdic.Add("awardProb", DR["awardProb"].ToString());  //得奖概率
            tempdic.Add("awardType", DR["awardType"].ToString());  //奖品类型 1、金币；2、房卡；3、话费
            tempdic.Add("awardSort", DR["awardSort"].ToString());  //商品序列
            tempdic.Add("awardName", DR["awardName"].ToString());  //奖品名称
            tempdic.Add("awardGold", DR["awardGold"].ToString());  //奖品兑换金币数
            //tempdic.Add("awardLottery", DR["awardLottery"].ToString());  //奖品兑换奖券数
            tempdic.Add("awardRoomCard", DR["awardRoomCard"].ToString());  //奖品兑换房卡数
            tempdic.Add("awardHuafei", DR["awardHuafei"].ToString());  //奖品兑换话费
            tempdic.Add("awardImg", DR["awardImg"].ToString());  //奖品图片

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 抽奖结果
    protected void LotteryResult()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid"); //用户ID
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        

        DataTable DataDT = SimonDB.DataTable(@"select * from Web_LotteryAward order by awardSort");
        int count = DataDT.Rows.Count;
        float[] prob = new float[count];
        for (int i = 0; i < count; i++)
        {
            float f = float.Parse(DataDT.Rows[i]["awardProb"].ToString());
            prob[i] = f;
        }
        //float[] prob = new float[4]{
        //        0.980f,
        //        0.550f,
        //        0.230f,
        //        0.010f
        //    };
        int result = 0;
        Random rnd = new Random();
        Random r = rnd;
        int n = (int)(prob.Sum() * 1000);           //计算概率总和，放大1000倍
        float x = (float)r.Next(0, n) / 1000;       //随机生成0~概率总和的数字

        for (int i = 0; i < prob.Count(); i++)
        {
            float pre = prob.Take(i).Sum();         //区间下界
            float next = prob.Take(i + 1).Sum();    //区间上界
            if (x >= pre && x < next)               //如果在该区间范围内，就返回结果退出循环
            {
                result = i;                      //本次抽奖结果
                break;
            }
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@awardid", DataDT.Rows[result]["awardid"].ToString()));
        string orderNum = "LA" + DateTime.Now.ToString("yyyyMMddmmssfff");
        lpar.Add(SimonDB.CreDbPar("@orderNum", orderNum));

        int UsedTimes = (int)SimonDB.ExecuteScalar(@"select count(1) from Web_LotteryLog where userid=@userid and DateDiff(dd,addTime,getdate())=0", lpar.ToArray());
        int fenxiangTimes = (int)SimonDB.ExecuteScalar(@"select count(1) from Web_LotteryAwardTimes where userid=@userid and DateDiff(dd,addTime,getdate())=0", lpar.ToArray());
        int morenTimes = 1;

        int Times = morenTimes + fenxiangTimes - UsedTimes;

        if (Times>0)
        {
            SimonDB.ExecuteNonQuery(@"insert into Web_LotteryLog values(@userid,@orderNum,@awardid,0,0,getdate())", lpar.ToArray());
        }
        else
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无可用抽奖次数"));
        }


        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["awardid"] = DataDT.Rows[result]["awardid"].ToString();
        jd["results"]["awardName"] = DataDT.Rows[result]["awardName"].ToString();
        jd["results"]["orderNum"] = orderNum;

        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    #region 领取抽奖结果
    protected void GetLotteryAward()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid"); //用户ID
        string ordernum = Request.Params["ordernum"]; //订单号

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (ordernum.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单号有误"));
        }

        if ((int)SimonDB.ExecuteScalar(@"select count(1) from Web_LotteryLog where orderNum=@orderNum and isGet=1", new DbParameter[] { SimonDB.CreDbPar("@orderNum", ordernum) }) >0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "奖品已被领取"));
        }

        DataTable DataDT = SimonDB.DataTable(@"select * from Web_LotteryLog where orderNum=@orderNum", new DbParameter[] { SimonDB.CreDbPar("@orderNum", ordernum) });

        string awardid = DataDT.Rows[0]["awardid"].ToString();

        DataTable AwardDataDT = SimonDB.DataTable(@"select * from Web_LotteryAward where awardid=@awardid", new DbParameter[] { SimonDB.CreDbPar("@awardid", awardid) });

        Int64 changeGold = Convert.ToInt64(AwardDataDT.Rows[0]["awardGold"]);
        Int64 changeRoomCard = Convert.ToInt64(AwardDataDT.Rows[0]["awardRoomCard"]);
        Int64 changeHuafei = Convert.ToInt64(AwardDataDT.Rows[0]["awardHuafei"]);
        // Int64 changeLottery = Convert.ToInt64(AwardDataDT.Rows[0]["awardLottery"]);

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@ordernum", ordernum));
        lpar.Add(SimonDB.CreDbPar("@changeGold", changeGold));
        lpar.Add(SimonDB.CreDbPar("@changeRoomCard", changeRoomCard));
        lpar.Add(SimonDB.CreDbPar("@changeHuafei", changeHuafei));
        //lpar.Add(SimonDB.CreDbPar("@changeLottery",changeLottery));

        if (changeGold > 0)
        {
            SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog values(@userid,(select UserName from TUsers where userid=@userid),(select WalletMoney from TUserInfo where UserID=@userid),@changeGold,66,0,getdate(),'轮盘抽奖')", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@changeGold where userid=@userid", lpar.ToArray());
        }
        if (changeRoomCard > 0)
        {
            int startnum = (int)SimonDB.ExecuteScalar("select Roomcard from TUserInfo where userid=@userid", lpar.ToArray());
            lpar.Add(SimonDB.CreDbPar("@StartNum", startnum));
            SimonDB.ExecuteNonQuery(@"insert into FangkaRecord values(@userid, @ordernum, 1, @StartNum, @changeRoomCard, 4, GETDATE(), '轮盘抽奖')", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"update TUserInfo set RoomCard=RoomCard+@changeRoomCard where userid=@userid", lpar.ToArray());
        }
        if (changeHuafei > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "话费请联系客服领取"));
        }
        //if (changeLottery>0)
        //{
        //    SimonDB.ExecuteNonQuery(@"insert into Web_LotteriesLog values(@userid,(select UserName from TUsers where userid=@userid),(select Lotteries from Web_Users where UserID=@userid),@changeLottery,(select Lotteries+@changeLottery from Web_Users where UserID=@userid),getdate(),'轮盘抽奖',68)", lpar.ToArray());
        //    SimonDB.ExecuteNonQuery(@"update Web_Users set Lotteries=Lotteries+@changeLottery where UserID=@userid", lpar.ToArray());
        //}
        SimonDB.ExecuteNonQuery(@"update Web_LotteryLog set isGet=1 where orderNum=@orderNum", new DbParameter[] { SimonDB.CreDbPar("@orderNum", ordernum) });

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";

        SimonUtils.RespWNC(jd.ToJson());
    }

    #endregion

    #region 玩家轮盘抽奖获奖记录
    protected void GetUserLotteryLog()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid"); //用户ID
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        DataTable DataDT = SimonDB.DataTable(@"select a.*,b.awardName from Web_LotteryLog a left join Web_LotteryAward b on b.awardid=a.awardid where a.userid=@userid order by a.addTime desc", new DbParameter[] { SimonDB.CreDbPar("@userid",userid)});

        int RecordCount = DataDT.Rows.Count;
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("userid", DR["awardid"].ToString());  //玩家ID
            tempdic.Add("addTime", DR["awardProb"].ToString());  //得奖时间
            tempdic.Add("awardid", DR["awardType"].ToString());  //奖品ID
            tempdic.Add("awardName", DR["awardName"].ToString());  //奖品名称
            tempdic.Add("orderNum", DR["awardSort"].ToString());  //订单号
            tempdic.Add("isGet", DR["awardName"].ToString());  //是否已领取，0未领取，1已领取
            tempdic.Add("enable", DR["awardGold"].ToString());  //是否可领取，0可以，1不可以

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 增加轮盘抽奖次数（即记录分享时间，默认分享获取次数为1）
    protected void AddAwardTimes()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid"); //用户ID

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));


        if ((int)SimonDB.ExecuteScalar(@"select count(1) from Web_LotteryAwardTimes where userid=@userid and DateDiff(dd,AddTime,getdate())=0", lpar.ToArray()) > 0)
        {
            //SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您今日已经获取过分享增加次数了"));
        }
        else
        {
            SimonDB.ExecuteNonQuery(@"insert into Web_LotteryAwardTimes values (@userid,1,getdate())", lpar.ToArray());
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 获取用户轮盘抽奖次数
    protected void GetAwardTimes()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid"); //用户ID

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));

        int UsedTimes = (int)SimonDB.ExecuteScalar(@"select count(1) from Web_LotteryLog where userid=@userid and DateDiff(dd,addTime,getdate())=0", lpar.ToArray());
        int fenxiangTimes = (int)SimonDB.ExecuteScalar(@"select count(1) from Web_LotteryAwardTimes where userid=@userid and DateDiff(dd,addTime,getdate())=0", lpar.ToArray());
        int morenTimes = 1;

        int Times = morenTimes + fenxiangTimes - UsedTimes;

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["awardTimes"] = Times.ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 7日签到奖品列表
    protected void GetSignInAwardList()
    {
        CheckSign();
        DataTable ListDT = SimonDB.DataTable(@"select * from Web_SignInAward order by awardSort");
        int RecordCount = ListDT.Rows.Count;
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("awardid", DR["awardid"].ToString());  //奖品id
            tempdic.Add("awardType", DR["awardType"].ToString());  //奖品类型 1、金币；2、奖券；3、金币+奖券
            tempdic.Add("awardSort", DR["awardSort"].ToString());  //商品序列（天数）
            tempdic.Add("awardName", DR["awardName"].ToString());  //奖品名称
            tempdic.Add("awardGold", DR["awardGold"].ToString());  //奖品兑换金币数
            tempdic.Add("awardLottery", DR["awardLottery"].ToString());  //奖品兑换奖券数
            tempdic.Add("awardImg", DR["awardImg"].ToString());  //奖品图片

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 7日签到统计数据
    protected void SignInData()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid"); //用户ID

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        DataTable DataDT = SimonDB.DataTable(@"select * from Web_SignInLog where userid=@userid and DateDiff(week,addTime,getdate())=0", lpar.ToArray());

        int isHasSign = (int)SimonDB.ExecuteScalar(@"select count(1) from Web_SignInLog where userid=@userid and DateDiff(dd,addTime,getdate())=0", lpar.ToArray());

        int RecordCount = DataDT.Rows.Count;   //一共几天已签到

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("userid", DR["userid"].ToString());  //玩家
            tempdic.Add("addTime", DR["addTime"].ToString());  //签到时间
            tempdic.Add("days", DR["days"].ToString());  //第几天的签到
            tempdic.Add("awardid", DR["awardid"].ToString());  //签到奖品
            tempdic.Add("isGet", DR["isGet"].ToString());  //是否被领取（0，未领取；1，已领取）
            tempdic.Add("enable", DR["enable"].ToString());  //是否可领取（0，可领取；1、不可领取）

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("isHasSign", isHasSign);
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));

    }
    #endregion

    #region 签到奖品领取
    protected void GetSignInAward()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid"); //用户ID

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        //string nowdate = DateTime.Now.ToString("yyyy-MM-dd");
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        //lpar.Add(SimonDB.CreDbPar("@kw", "%" + nowdate + "%"));

        if ((int)SimonDB.ExecuteScalar(@"select count(1) from Web_SignInLog where userid=@userid and DATEDIFF(dd,addTime,getdate())=0", lpar.ToArray()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "今日已签到"));
        }

        int days = 0;

        days = (int)SimonDB.ExecuteScalar(@"select count(1) from Web_SignInLog where userid=@userid and DATEDIFF(week,addTime,getdate())=0", lpar.ToArray());

        //DayOfWeek day = DateTime.Now.DayOfWeek;
        //string dayString = day.ToString();

        //switch (dayString)
        //{
        //    case "Monday":
        //        //若就是星期一，那本周开头一天就是这个传进来的变量
        //        days = 1;
        //        break;
        //    case "Tuesday":
        //        //以此类推
        //        days = 2;
        //        break;
        //    case "Wednesday":
        //        days = 3;
        //        break;
        //    case "Thursday":
        //        days = 4;
        //        break;
        //    case "Friday":
        //        days = 5;
        //        break;
        //    case "Saturday":
        //        days = 6;
        //        break;
        //    case "Sunday":
        //        days = 7;
        //        break;
        //}
        lpar.Add(SimonDB.CreDbPar("@days", days + 1));
        SimonDB.ExecuteNonQuery(@"insert into Web_SignInLog values(@userid,getdate(),@days,(select awardid from Web_SignInAward where awardSort=@days),0,0)", lpar.ToArray());
        DataTable DataDT = SimonDB.DataTable(@"select * from Web_SignInLog a left join Web_SignInAward b on b.awardid=a.awardid where a.userid=@userid and DATEDIFF(day,a.addTime,getdate())=0", lpar.ToArray());


        Int64 changeGold = Convert.ToInt64(DataDT.Rows[0]["awardGold"]);
        Int64 changeLottery = Convert.ToInt64(DataDT.Rows[0]["awardLottery"]);
        lpar.Add(SimonDB.CreDbPar("@changeGold", changeGold));
        lpar.Add(SimonDB.CreDbPar("@changeLottery", changeLottery));

        if (changeGold > 0)
        {
            SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog values(@userid,(select UserName from TUsers where userid=@userid),(select WalletMoney from TUserInfo where UserID=@userid),@changeGold,67,0,getdate(),'签到奖励')", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@changeGold where userid=@userid", lpar.ToArray());
        }
        if (changeLottery > 0)
        {
            SimonDB.ExecuteNonQuery(@"insert into Web_LotteriesLog values(@userid,(select UserName from TUsers where userid=@userid),(select Lotteries from Web_Users where UserID=@userid),@changeLottery,(select Lotteries+@changeLottery from Web_Users where UserID=@userid),getdate(),'签到奖励',69)", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"update Web_Users set Lotteries=Lotteries+@changeLottery where UserID=@userid", lpar.ToArray());
        }
        SimonDB.ExecuteNonQuery(@"update Web_SignInLog set isGet=1 where DateDiff(dd,addTime,getdate())=0", lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("userid", DR["userid"].ToString());  //玩家
            tempdic.Add("addTime", DR["addTime"].ToString());  //签到时间
            tempdic.Add("days", DR["days"].ToString());  //第几天的签到
            tempdic.Add("awardid", DR["awardid"].ToString());  //签到奖品ID
            tempdic.Add("awardName", DR["awardName"].ToString());  //奖品名称
            tempdic.Add("awardGold", DR["awardGold"].ToString());  //奖品兑换金币数
            tempdic.Add("awardLottery", DR["awardLottery"].ToString());//奖品兑换奖券数

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));

    }
    #endregion

    #region 房卡游戏加入房间
    protected void GetFangKaRoomInfo()
    {
        CheckSign();
        string RoomNum = Request.Params["roomnum"];
        string UserID = Request.Params["userid"];
        if (RoomNum.Length < 1 || !SimonUtils.IsNum(RoomNum))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间号错误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();

        lpar.Add(SimonDB.CreDbPar("@RoomNum", RoomNum));

        DataTable DT = SimonDB.DataTable(@"select * from FangkaRoomInfo where RoomNum=@RoomNum", lpar.ToArray());
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间不存在"));
        }

        DataRow DR = DT.Rows[0];

        string roomid = DR["RoomID"].ToString();
        string payrule = DR["PayRule"].ToString();
        string status = DR["Status"].ToString();   // 0-游戏未开始 1-游戏已结束 2-正在游戏中 3-玩家已解散 4-管理员已解散
        string clubid = DR["CreateClubID"].ToString();

        int jushu = Convert.ToInt32(DR["JuShuRule"]);
        lpar.Add(SimonDB.CreDbPar("@jushu", jushu));
        lpar.Add(SimonDB.CreDbPar("@roomid", roomid));
        lpar.Add(SimonDB.CreDbPar("@payrule", payrule));
        lpar.Add(SimonDB.CreDbPar("@userid", UserID));
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));

        if ((int)SimonDB.ExecuteScalar("select count(1) from ClubUser where UserID=@userid and ClubID=@clubid and Status=1", lpar.ToArray()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您已被管理员封印"));
        }


        if (status == "1")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏已结束"));
        }
        //if (status == "2")
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "正在游戏中"));
        //}
        if (status == "3")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家已解散"));
        }
        if (status == "3")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "管理员已解散"));
        }


        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where ClubID=@clubid and UserID=@userid", lpar.ToArray()) <= 0 && clubid != "0")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间为俱乐部专属，您不是该俱乐部会员"));
        }
        //DataTable PayRuleDT= SimonDB.DataTable(@"select * from FangkaPayRule where RoomID=@roomid and PayRule=@payrule", lpar.ToArray());
        DataTable PayRuleDT = SimonDB.DataTable(@"select * from FangkaPayRule2 where RoomID=@roomid and PayRule=@payrule and Jushu=@jushu", lpar.ToArray());

        if (PayRuleDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间支付规则有误"));
        }

        //int singleCount = Convert.ToInt32(PayRuleDT.Rows[0]["PayCount"]);

        int Count = Convert.ToInt32(PayRuleDT.Rows[0]["PayCount"]);

        //int NeedRoomCard = 0;

        if (payrule == "1")    //房主支付
        {

        }

        //if (payrule == "2")   //AA支付
        //{
        //    NeedRoomCard = singleCount * jushu;

        //    DataTable UserDT = SimonDB.DataTable(@"select * from TUserInfo where UserID=@userid", lpar.ToArray());
        //    int RoomCard = Convert.ToInt32(UserDT.Rows[0]["RoomCard"]);
        //    if (RoomCard < NeedRoomCard)
        //    {
        //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "可支付房卡数不足"));
        //    }
        //}

        if (payrule == "2" || payrule == "3")   //AA支付
        {
            DataTable UserDT = SimonDB.DataTable(@"select * from TUserInfo where UserID=@userid", lpar.ToArray());
            int RoomCard = Convert.ToInt32(UserDT.Rows[0]["RoomCard"]);
            if (RoomCard < Count)
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "可支付房卡数不足"));
            }
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["RoomID"] = DR["RoomID"].ToString();
        jd["results"]["RoomNum"] = DR["RoomNum"].ToString();
        jd["results"]["RecordNum"] = DR["RecordNum"].ToString();
        jd["results"]["GameNameID"] = DR["GameNameID"].ToString();
        jd["results"]["Desk"] = DR["Desk"].ToString();
        jd["results"]["PayRule"] = DR["PayRule"].ToString();
        jd["results"]["GameRule"] = DR["GameRule"].ToString();
        jd["results"]["JuShuRule"] = DR["JuShuRule"].ToString();
        jd["results"]["PlayersRule"] = DR["PlayersRule"].ToString();
        jd["results"]["CreateTime"] = DR["CreateTime"].ToString();
        jd["results"]["Rate"] = DR["Rate"].ToString();
        jd["results"]["OwnerID"] = DR["OwnerID"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 房卡总战绩
    protected void FangkaRecord()
    {
        CheckSign();
        string recordNum = Request.Params["recordNum"];
        if (recordNum.Length < 1 || !SimonUtils.IsNum(recordNum))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "记录编号有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@recordNum", recordNum));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameRecord where RecordNum=@recordNum", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "记录编号不存在"));
        }

        DataTable DataDT = SimonDB.DataTable(@"select a.*,b.GameNameID,b.RoomNum,b.GameRule,b.JuShuRule,b.Rate,b.OwnerID,c.HeadIconUrl,d.NickName,b.CountRule,f.MaxRound from FangkaGameRecord a left join FangkaRoomInfo b on b.RecordNum=a.RecordNum left join Web_UserWeixin c on c.userid=a.userid left join TUsers d on d.userid=a.userid   left join (select RecordNum,MAX(RoundNum) as MaxRound from FangkaGameSingleRecord group by RecordNum) f on f.RecordNum=a.RecordNum where a.RecordNum=@recordNum", lpar.ToArray());

        int RecordCount = DataDT.Rows.Count;   //一共几条记录

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RecordNum", DR["RecordNum"].ToString());  //记录编号
            tempdic.Add("GameNameID", DR["GameNameID"].ToString());  //游戏ID
            tempdic.Add("RoomNum", DR["RoomNum"].ToString());  //房间号
            tempdic.Add("CountRule", DR["CountRule"].ToString());  //记局规则：1按局，2按圈
            tempdic.Add("JuShuRule", DR["JuShuRule"].ToString());  //游戏总局数
            tempdic.Add("MaxRound", DR["MaxRound"].ToString());  //结算局数
            tempdic.Add("Rate", DR["Rate"].ToString());  //游戏倍率
            tempdic.Add("OwnerID", DR["OwnerID"].ToString());  //房主ID
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称
            tempdic.Add("HeadIconUrl", DR["HeadIconUrl"].ToString());  //玩家头像
            tempdic.Add("ScoreSum", DR["ScoreSum"].ToString());  //单人总结算分
            tempdic.Add("IsWinUser", DR["IsWinUser"].ToString());  //是否为大赢家，0否，1是
            tempdic.Add("IsDissolve", DR["IsDissolve"].ToString());  //是否为发起解散者，0否，1是
            tempdic.Add("StartTime", DR["StartTime"].ToString());  //游戏开始时间
            tempdic.Add("AddTime", DR["AddTime"].ToString());  //游戏总结算时间
            tempdic.Add("GameRule", DR["GameRule"].ToString());  //房间规则
            tempdic.Add("Score1", DR["Score1"].ToString());  //外包分
            tempdic.Add("BackYards", DR["BackYards"].ToString());  //回放码

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 房卡总战绩（单人历史）
    protected void FangkaRecordByUser()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();
        if (userid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@userid", userid));
            lwhere.Add("a.userid=@userid");
        }

        string _countsql = @"select count(1) from FangkaGameRecord a {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by AddTime desc) as row, * from (select top 60 a.*,b.GameNameID,b.RoomNum,b.JuShuRule,b.Rate,b.OwnerID from FangkaGameRecord a left join FangkaRoomInfo b on b.RecordNum=a.RecordNum {0} order by a.AddTime desc) as datatb
                                ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        if (RecordCount > 60)
        {
            RecordCount = 60;
        }
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "5";  //默认每页5条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RecordNum", DR["RecordNum"].ToString());  //记录编号
            tempdic.Add("GameNameID", DR["GameNameID"].ToString());  //游戏ID
            tempdic.Add("RoomNum", DR["RoomNum"].ToString());  //房间号
            tempdic.Add("JuShuRule", DR["JuShuRule"].ToString());  //游戏总局数
            tempdic.Add("Rate", DR["Rate"].ToString());  //游戏倍率
            tempdic.Add("OwnerID", DR["OwnerID"].ToString());  //房主ID
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("ScoreSum", DR["ScoreSum"].ToString());  //单人总结算分
            tempdic.Add("IsWinUser", DR["IsWinUser"].ToString());  //是否为大赢家，0否，1是
            tempdic.Add("IsDissolve", DR["IsDissolve"].ToString());  //是否为发起解散者，0否，1是
            tempdic.Add("StartTime", DR["StartTime"].ToString());  //游戏开始时间
            tempdic.Add("AddTime", DR["AddTime"].ToString());  //游戏总结算时间

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 房卡每局战绩
    protected void FangkaRecordsingle()
    {
        CheckSign();
        string recordNum = Request.Params["recordNum"];
        string roundNum = Request.Params["roundNum"];
        if (recordNum.Length < 1 || !SimonUtils.IsNum(recordNum))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "记录编号有误"));
        }
        if (roundNum.Length < 1 || !SimonUtils.IsNum(roundNum))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "局号有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@recordNum", recordNum));
        lpar.Add(SimonDB.CreDbPar("@roundNum", roundNum));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameSingleRecord where RecordNum=@recordNum and RoundNum=@roundNum", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该局战绩记录不存在"));
        }
        DataTable DataDT = SimonDB.DataTable(@"select * from FangkaGameSingleRecord x left join Web_UserWeixin a on a.userid=x.userid left join TUsers b on b.userid=x.userid where x.RecordNum=@recordNum and x.RoundNum=@roundNum", lpar.ToArray());

        int RecordCount = DataDT.Rows.Count;   //一共几条记录

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RecordNum", DR["RecordNum"].ToString());  //记录编号
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称
            tempdic.Add("HeadIconUrl", DR["HeadIconUrl"].ToString());  //玩家头像
            tempdic.Add("RoundNum", DR["RoundNum"].ToString());  //局号
            tempdic.Add("Score", DR["Score"].ToString());  //单局结算分
            tempdic.Add("HandPatterns", DR["HandPatterns"].ToString());  //手牌数据
            tempdic.Add("XiPai", DR["XiPai"].ToString());  //喜牌
            tempdic.Add("IsSurrender", DR["IsSurrender"].ToString());  //是否投降，0否，1是
            tempdic.Add("AddTime", DR["AddTime"].ToString());  //单局结算时间

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 房卡战绩（新修订）2019-11-26
    protected void GetUserFangkaData()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string clubid = Request.Params["clubid"];   
        string gameid = Request.Params["gameid"];

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }

        if (gameid.Length < 1 || !SimonUtils.IsNum(gameid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏ID有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
        lpar.Add(SimonDB.CreDbPar("@gameid", gameid));

        DataTable recordDataDT = SimonDB.DataTable(@"select RecordNum from FangkaGameRecord where RecordNum in (select RecordNum from FangkaRoomInfo where GameNameID=@gameid and CreateClubID=@clubid) and UserID=@userid", lpar.ToArray());

        
        //lpar.Add(SimonDB.CreDbPar("@recordNum", recordNum));
        //if ((int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameRecord where RecordNum=@recordNum", lpar.ToArray()) <= 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "记录编号不存在"));
        //}

        DataTable DataDT = SimonDB.DataTable(@"select a.*,b.GameNameID,b.RoomNum,b.GameRule,b.JuShuRule,b.Rate,b.OwnerID,c.HeadIconUrl,d.NickName,b.CountRule,f.MaxRound from FangkaGameRecord a left join FangkaRoomInfo b on b.RecordNum=a.RecordNum left join Web_UserWeixin c on c.userid=a.userid left join TUsers d on d.userid=a.userid   left join (select RecordNum,MAX(RoundNum) as MaxRound from FangkaGameSingleRecord group by RecordNum) f on f.RecordNum=a.RecordNum where a.RecordNum=@recordNum", lpar.ToArray());

        int RecordCount = DataDT.Rows.Count;   //一共几条记录

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RecordNum", DR["RecordNum"].ToString());  //记录编号
            tempdic.Add("GameNameID", DR["GameNameID"].ToString());  //游戏ID
            tempdic.Add("RoomNum", DR["RoomNum"].ToString());  //房间号
            tempdic.Add("CountRule", DR["CountRule"].ToString());  //记局规则：1按局，2按圈
            tempdic.Add("JuShuRule", DR["JuShuRule"].ToString());  //游戏总局数
            tempdic.Add("MaxRound", DR["MaxRound"].ToString());  //结算局数
            tempdic.Add("Rate", DR["Rate"].ToString());  //游戏倍率
            tempdic.Add("OwnerID", DR["OwnerID"].ToString());  //房主ID
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称
            tempdic.Add("HeadIconUrl", DR["HeadIconUrl"].ToString());  //玩家头像
            tempdic.Add("ScoreSum", DR["ScoreSum"].ToString());  //单人总结算分
            tempdic.Add("IsWinUser", DR["IsWinUser"].ToString());  //是否为大赢家，0否，1是
            tempdic.Add("IsDissolve", DR["IsDissolve"].ToString());  //是否为发起解散者，0否，1是
            tempdic.Add("StartTime", DR["StartTime"].ToString());  //游戏开始时间
            tempdic.Add("AddTime", DR["AddTime"].ToString());  //游戏总结算时间
            tempdic.Add("GameRule", DR["GameRule"].ToString());  //房间规则
            tempdic.Add("Score1", DR["Score1"].ToString());  //外包分
            tempdic.Add("BackYards", DR["BackYards"].ToString());  //回放码

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 根据玩家ID显示头像和昵称
    protected void GetUserWechatMsg()
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid"); //用户ID

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        DataTable DT = SimonDB.DataTable(@"select * from TUsers a left join Web_UserWeixin b on b.userid=a.userid where a.userid=@userid", new DbParameter[] { SimonDB.CreDbPar("@userid", userid) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家信息不存在"));
        }
        DataRow DR = DT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["NickName"] = DR["NickName"].ToString();
        jd["results"]["HeadIconUrl"] = DR["HeadIconUrl"].ToString();
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 申请创建俱乐部
    protected void ClubCreate()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string phone = Request.Params["phone"];
        string remark = Request.Params["remark"];
        string name = Request.Params["name"];
        string gameid = Request.Params["gameid"];
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (gameid.Length < 1 || !SimonUtils.IsNum(gameid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏ID有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@phone", phone));
        lpar.Add(SimonDB.CreDbPar("@remark", remark));
        lpar.Add(SimonDB.CreDbPar("@name", name));
        lpar.Add(SimonDB.CreDbPar("@gameid", gameid));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from TUsers where UserID=@userid", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID不存在"));
        }
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubCreateApply where ApplyName=@name", lpar.ToArray()) > 0 || (int)SimonDB.ExecuteScalar(@"select count(1) from ClubInfo where ClubName=@name", lpar.ToArray()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部名字已存在，请修改其他的吧"));
        }
        DataTable DataDT = SimonDB.DataTable(@"select top 1 * from ClubConfig");
        if (DataDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "创建俱乐部设置参数有误，请联系客服"));
        }
        if (!SimonUtils.CheckPhoneIsAble(phone))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "手机号码格式有误"));
        }
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubAgent where phone=@phone", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "对不起，您还不是代理，请联系客服开通代理账号。"));
        }

        //生成系统指定俱乐部ID位数的ClubID
        int ClubIDLength = Convert.ToInt32(DataDT.Rows[0]["ClubIDNum"]);
        string buffer = "0123456789";// 随机字符中也可以为汉字（任何）
        StringBuilder sb = new StringBuilder();
        Random r = new Random();
        int range = buffer.Length;
        for (int i = 0; i < ClubIDLength; i++)
        {
            sb.Append(buffer.Substring(r.Next(range), 1));
        }
        string ClubID = sb.ToString();
        if (Convert.ToInt32(ClubID) < 100000)
        {
            ClubID = Convert.ToInt32(ClubID).ToString() + "0";
        }

        lpar.Add(SimonDB.CreDbPar("@clubid", ClubID));
        //SimonDB.ExecuteNonQuery(@"insert into ClubCreateApply values(@clubid,@userid,@phone,@remark,@name,getdate(),0)",lpar.ToArray());
        lpar.Add(SimonDB.CreDbPar("@password", SimonUtils.EnCodeMD5("123456")));



        if (CurrSite.EnableCreateClub)
        {
            SimonDB.ExecuteNonQuery(@"insert into ClubCreateApply values(@clubid,@userid,@phone,@remark,@name,getdate(),0,@gameid)", lpar.ToArray());
            //SimonDB.ExecuteNonQuery(@"insert into ClubInfo values(@clubid,@password,0,@userid,@name,1,@remark,getdate(),1)", lpar.ToArray());
            //SimonDB.ExecuteNonQuery(@"insert into ClubUser values(@clubid,@userid,1,getdate())", lpar.ToArray());
        }
        else
        {
            DataTable RoomIDDataDT = SimonDB.DataTable(@"select * from TGameRoomInfo where GameNameID=@gameid", lpar.ToArray());

            if (RoomIDDataDT.Rows.Count > 0)
            {
                string RoomID = RoomIDDataDT.Rows[0]["RoomID"].ToString();
                lpar.Add(SimonDB.CreDbPar("@RoomID", RoomID));

                DataTable ServerDataDT = SimonDB.DataTable(@"select RoomID as ServerID from TGameRoomInfo where RoomID!=@RoomID and RoomID not in (select ServerID from (select ServerID,COUNT(*) as num from ClubInfo group by ServerID)b where b.num>=2) and GameNameID=@gameid", lpar.ToArray());

                if (ServerDataDT.Rows.Count > 0)
                {
                    string serverID = ServerDataDT.Rows[0]["ServerID"].ToString();
                    lpar.Add(SimonDB.CreDbPar("@serverID", serverID));
                }
                else
                {
                    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无可支配空间错误"));
                }

                SimonDB.ExecuteNonQuery(@"insert into ClubCreateApply values(@clubid,@userid,@phone,@remark,@name,getdate(),1,@gameid)", lpar.ToArray());
                SimonDB.ExecuteNonQuery(@"insert into ClubInfo values(@clubid,@password,0,@userid,@name,1,@remark,getdate(),0,@serverID,0)", lpar.ToArray());
                if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubAgent where phone=(select Phone from ClubCreateApply where  ClubID=@clubid)", lpar.ToArray()) > 0)
                {

                }
                else
                {
                    SimonDB.ExecuteNonQuery(@"insert into ClubAgent select Phone,'E10ADC3949BA59ABBE56E057F20F883E',0,getdate(),0 from ClubCreateApply where ClubID=@clubid", lpar.ToArray());
                }

                SimonDB.ExecuteNonQuery(@"insert into ClubUser values(@clubid,@userid,1,getdate(),0,null,0,0)", lpar.ToArray());

                //if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubCreateApply where IsPass=0 and ClubID=@clubid", lpar.ToArray()) > 0)
                //{

                //}
                //else
                //{
                //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该条申请已被处理"));
                //}

            }
            else
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无可支配空间错误"));
            }

        }


        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["clubid"] = ClubID.ToString();
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 申请/邀请加入俱乐部
    protected void ClubJoin()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];
        string userid = Request.Params["userid"];
        string type = Request.Params["type"];   //1申请，2受邀，3，申请退出
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
        lpar.Add(SimonDB.CreDbPar("@type", type));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from TUsers where UserID=@userid", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID不存在"));
        }
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubInfo where ClubID=@clubid and ClubStatus=0", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID不存在"));
        }
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubApply where UserID=@userid and ClubID=@clubid and IsPass=0", lpar.ToArray()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您已提交过申请，请等待审核"));
        }
        if (type != "3")
        {
            if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where UserID=@userid and ClubID=@clubid", lpar.ToArray()) > 0)
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您已加入该俱乐部"));
            }


            if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where UserID=@userid and ClubID!=@clubid", lpar.ToArray()) > 0)
            {
                //本段代码代表玩家可以加入不同俱乐部
                if (CurrSite.EnableJoinOtherClub)
                {
                    if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where UserID=@userid and ClubID!=@clubid", lpar.ToArray()) >= Convert.ToInt32(CurrSite.GetAppSettings("joinotherclub")))
                    {
                        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您已超过加入俱乐部上限"));
                    }

                }
                else
                {
                    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您已加入其他俱乐部"));
                }

            }

            //SimonDB.ExecuteNonQuery(@"insert into ClubApply values(@clubid,@userid,@type,getdate(),0,0)",lpar.ToArray());
            if (CurrSite.EnableJoinClub)
            {
                SimonDB.ExecuteNonQuery(@"insert into ClubApply values(@clubid,@userid,@type,getdate(),0,0)", lpar.ToArray());
                //SimonDB.ExecuteNonQuery(@"insert into ClubUser values(@clubid,@userid,3,getdate())", lpar.ToArray());
            }
            else
            {
                SimonDB.ExecuteNonQuery(@"insert into ClubApply values(@clubid,@userid,@type,getdate(),1,1)", lpar.ToArray());
                SimonDB.ExecuteNonQuery(@"insert into ClubUser values(@clubid,@userid,4,getdate(),0,null,0,0)", lpar.ToArray());
            }

        }
        else
        {
            SimonDB.ExecuteNonQuery(@"insert into ClubApply values(@clubid,@userid,@type,getdate(),0,0)", lpar.ToArray());

        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 俱乐部成员列表
    protected void GetClubUserList()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];
        string userid = Request.Params["userid"];
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID格式有误"));
        }
        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();
        if (userid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@userid", userid));
        }
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
        lwhere.Add(" a.ClubID=@clubid");

        int duty = (int)SimonDB.ExecuteScalar(@"select ClubDuty from ClubUser where UserID=@userid and ClubID=@clubid", lpar.ToArray());

        string _countsql = @"select count(1) from ClubUser a {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by ClubDuty) as row, * from (select a.*,b.HeadIconUrl,c.NickName from ClubUser a left join Web_UserWeixin b on b.userid=a.userid left join TUsers c on c.userid=a.userid {0} ) as datatb) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());

        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        //if (pageindex == "0") pageindex = "1";  //默认第1页
        //if (pagesize == "0") pagesize = "5";  //默认每页5条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());


        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("ClubID", DR["ClubID"].ToString());  //俱乐部ID
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称
            tempdic.Add("HeadIconUrl", DR["HeadIconUrl"].ToString());  //玩家头像
            tempdic.Add("ClubDuty", DR["ClubDuty"].ToString());  //俱乐部等级，1会长，2值班，3管理，4会员
            tempdic.Add("JoinTime", DR["JoinTime"].ToString());  //加入时间
            tempdic.Add("Status", DR["Status"].ToString());  //玩家状态：0正常，1禁止游戏
            tempdic.Add("ResetTime", DR["ResetTime"].ToString());  //上次清零时间

            List<DbParameter> lpar1 = new List<DbParameter>();
            lpar1.Add(SimonDB.CreDbPar("@ResetTime", DR["ResetTime"].ToString()));
            lpar1.Add(SimonDB.CreDbPar("@UserID", DR["UserID"].ToString()));
            lpar1.Add(SimonDB.CreDbPar("@ClubID", DR["ClubID"].ToString()));
            tempdic.Add("Jushu", SimonDB.ExecuteScalar(@"select count(1) from FangkaGameRecord where RecordNum in (select RecordNum from FangkaRoomInfo where CreateClubID=@ClubID) and UserID=@UserID and AddTime>@ResetTime", lpar1.ToArray()).ToString());  //有效局数

            tempdic.Add("IsOnline", SimonDB.ExecuteScalar(@"select count(1) from TWLoginRecord where UserID=@UserID", lpar1.ToArray()).ToString());  //玩家是否在线  0不在，1在
            if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where UserID=@UserID and ClubID=@ClubID and IsHehuo=1", lpar1.ToArray()) > 0)
            {
                tempdic.Add("IsHehuo", "1");
            }
            else
            {
                tempdic.Add("IsHehuo", "0");
            }

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("userduty", duty);
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 俱乐部权限操作
    protected void ClubLimitsSys()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];
        string userid = Request.Params["userid"];
        string operatorid = Request.Params["operatorid"];
        string sys = Request.Params["sys"];     //权限操作，1升职，2降职，3开除，4值班，5禁止游戏，6解除禁止，7数据清零，9设置归属
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID格式有误"));
        }
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID格式有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@operatorid", operatorid));
        lpar.Add(SimonDB.CreDbPar("@sys", sys));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where clubid=@clubid and userid=@userid", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部不存在该玩家信息"));
        }

        DataTable LimitsDataDT = SimonDB.DataTable(@"select * from ClubUserLimits where userid=@operatorid and clubid=@clubid", lpar.ToArray());

        int isAgreeJoin = 0;
        int isKickOut = 0;
        int isOpenRoom = 0;
        int isDissolve = 0;
        int isEmpty = 0;
        int isForbid = 0;
        int isCheck = 0;
        if (LimitsDataDT.Rows.Count>0)
        {
            isAgreeJoin = Convert.ToInt32(LimitsDataDT.Rows[0]["isAgreeJoin"]);
            isKickOut = Convert.ToInt32(LimitsDataDT.Rows[0]["isKickOut"]);
            isOpenRoom = Convert.ToInt32(LimitsDataDT.Rows[0]["isOpenRoom"]);
            isDissolve = Convert.ToInt32(LimitsDataDT.Rows[0]["isDissolve"]);
            isEmpty = Convert.ToInt32(LimitsDataDT.Rows[0]["isEmpty"]);
            isForbid = Convert.ToInt32(LimitsDataDT.Rows[0]["isForbid"]);
            isCheck = Convert.ToInt32(LimitsDataDT.Rows[0]["isCheck"]);
        }

        int opLimit = (int)SimonDB.ExecuteScalar(@"select ClubDuty from ClubUser where userid=@operatorid and clubid=@clubid", lpar.ToArray());

        int userOpLimit = (int)SimonDB.ExecuteScalar(@"select ClubDuty from ClubUser where userid=@userid and clubid=@clubid", lpar.ToArray());

        if (opLimit == 4 && sys != "9")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该级别无操作权限"));
        }

        Random rd = new Random();
        string newsid = DateTime.Now.ToString("yyyyMMddHHmmss") + rd.Next(0, 1000);
        lpar.Add(SimonDB.CreDbPar("@newsid", newsid));
        string newscontent = "已经被";
        lpar.Add(SimonDB.CreDbPar("@newscontent", newscontent));

        //升职
        if (sys == "1")
        {
            SimonDB.ExecuteNonQuery(@"update ClubUser set ClubDuty=3 where userid=@userid and clubid=@clubid and ClubDuty=4", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubSysLog values(@clubid,@userid,@operatorid,'升职操作',getdate())", lpar.ToArray());

            SimonDB.ExecuteNonQuery(@"insert into ClubNews values(@newsid,@userid,@clubid,@operatorid,@sys,@newscontent,getdate())", lpar.ToArray());
        }

        //降职
        if (sys == "2")
        {
            if (userOpLimit == 2)
            {
                SimonDB.ExecuteNonQuery(@"update ClubUser set ClubDuty=3 where userid=@userid and clubid=@clubid ", lpar.ToArray());
            }
            if (userOpLimit == 3)
            {
                SimonDB.ExecuteNonQuery(@"update ClubUser set ClubDuty=4 where userid=@userid and clubid=@clubid ", lpar.ToArray());
            }
            SimonDB.ExecuteNonQuery(@"insert into ClubSysLog values(@clubid,@userid,@operatorid,'降职操作',getdate())", lpar.ToArray());

            SimonDB.ExecuteNonQuery(@"insert into ClubNews values(@newsid,@userid,@clubid,@operatorid,@sys,@newscontent,getdate())", lpar.ToArray());
        }

        //开除
        //if (sys == "3" && isKickOut>0)
        if (sys == "3")
        {
            SimonDB.ExecuteNonQuery(@"delete ClubUser where userid=@userid and clubid=@clubid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubSysLog values(@clubid,@userid,@operatorid,'开除操作',getdate())", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubNews values(@newsid,@userid,@clubid,@operatorid,@sys,@newscontent,getdate())", lpar.ToArray());
        }

        //值班
        if (sys == "4")
        {
            SimonDB.ExecuteNonQuery(@"update ClubUser set ClubDuty=3 where ClubDuty=2 and clubid=@clubid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"update ClubUser set ClubDuty=2 where userid=@userid and clubid=@clubid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubSysLog values(@clubid,@userid,@operatorid,'设为值班',getdate())", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubNews values(@newsid,@userid,@clubid,@operatorid,@sys,@newscontent,getdate())", lpar.ToArray());
        }

        //禁止游戏
        //if (sys == "5" && isForbid > 0)
        if (sys == "5" )
        {
            SimonDB.ExecuteNonQuery(@"update ClubUser set Status=1 where userid=@userid and clubid=@clubid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubSysLog values(@clubid,@userid,@operatorid,'禁止游戏',getdate())", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubNews values(@newsid,@userid,@clubid,@operatorid,@sys,@newscontent,getdate())", lpar.ToArray());
        }

        //解除禁止
        //if (sys == "6" && isForbid > 0)
        if (sys == "6" )
        {
            SimonDB.ExecuteNonQuery(@"update ClubUser set Status=0 where userid=@userid and clubid=@clubid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubSysLog values(@clubid,@userid,@operatorid,'解除禁止',getdate())", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubNews values(@newsid,@userid,@clubid,@operatorid,@sys,@newscontent,getdate())", lpar.ToArray());
        }

        //数据清零
        //if (sys == "7"&& isEmpty>0)
        if (sys == "7" )
        {
            SimonDB.ExecuteNonQuery(@"update ClubUser set ResetTime=getdate() where userid=@userid and clubid=@clubid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubSysLog values(@clubid,@userid,@operatorid,'数据清零',getdate())", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into ClubNews values(@newsid,@userid,@clubid,@operatorid,@sys,@newscontent,getdate())", lpar.ToArray());
        }

        //合伙人归置
        if (sys == "9")
        {
            if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where clubid=@clubid and userid=@operatorid and IsHehuo=1", lpar.ToArray()) > 0)
            {
                SimonDB.ExecuteNonQuery(@"update ClubUser set PadreUserID=@operatorid where userid=@userid and clubid=@clubid", lpar.ToArray());
                SimonDB.ExecuteNonQuery(@"insert into ClubSysLog values(@clubid,@userid,@operatorid,'设置归属',getdate())", lpar.ToArray());

                SimonDB.ExecuteNonQuery(@"insert into ClubNews values(@newsid,@userid,@clubid,@operatorid,@sys,'已经被会长归置到',getdate())", lpar.ToArray());
            }
            else
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您输入的ID不是该俱乐部的合伙人"));
            }
        }



        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 俱乐部消息列表
    protected void ClubMsgList()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID格式有误"));
        }

        DataTable DT = SimonDB.DataTable(@"select * from ClubApply a  left join Web_UserWeixin b on b.userid=a.userid left join TUsers c on c.userid=a.userid where a.ClubID=@clubid and (a.IsRead=0 or (a.IsRead=1 and a.IsPass=0))", new DbParameter[] { SimonDB.CreDbPar("@clubid", clubid) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无数据"));
        }
        int RecordCount = DT.Rows.Count;   //一共几条记录

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("id", DR["id"].ToString());  //消息ID
            tempdic.Add("ClubID", DR["ClubID"].ToString());  //俱乐部ID
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称
            tempdic.Add("HeadIconUrl", DR["HeadIconUrl"].ToString());  //玩家头像
            tempdic.Add("Type", DR["Type"].ToString());  //消息类型：1、申请，2、被邀请
            tempdic.Add("ApplyTime", DR["ApplyTime"].ToString());  //申请时间
            tempdic.Add("IsRead", DR["IsRead"].ToString());  //是否已读
            tempdic.Add("IsPass", DR["IsPass"].ToString());  //是否通过

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 俱乐部消息处理
    protected void ClubMsgDeal()
    {
        CheckSign();
        string id = Request.Params["id"];
        string sys = Request.Params["sys"];     //消息操作，1同意，2拒绝
        if (id.Length < 1 || !SimonUtils.IsNum(id))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "消息ID格式有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@id", id));

        Random rd = new Random();
        string newsid = DateTime.Now.ToString("yyyyMMddHHmmss") + rd.Next(0, 1000);
        lpar.Add(SimonDB.CreDbPar("@newsid", newsid));

        DataTable DT = SimonDB.DataTable(@"select * from ClubInfo where ClubID=(select ClubID from ClubApply where id=@id)", lpar.ToArray());
        string clubname = DT.Rows[0]["ClubName"].ToString();


        DataTable DataDT = SimonDB.DataTable(@"select * from ClubApply where id=@id", lpar.ToArray());
        string type = DataDT.Rows[0]["Type"].ToString();

        if (sys == "1")
        {
            if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubApply where id=@id and IsPass!=0", lpar.ToArray()) > 0)
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您已处理过该信息！"));
            }
            else
            {

                if (type == "1")
                {
                    string newscontent = "申请加入俱乐部成功：管理同意了您加入[" + clubname + "]的申请";

                    lpar.Add(SimonDB.CreDbPar("@newscontent", newscontent));
                    lpar.Add(SimonDB.CreDbPar("@sys", "21"));

                    SimonDB.ExecuteNonQuery(@"update ClubApply set IsPass=1,IsRead=1 where id=@id", lpar.ToArray());
                    SimonDB.ExecuteNonQuery(@"insert into ClubUser select ClubID,UserID,4,getdate(),0,null,0,0 from ClubApply where id=@id", lpar.ToArray());
                }

                if (type == "3")
                {
                    string newscontent = "申请退出俱乐部成功：管理同意了您退出[" + clubname + "]的申请";

                    lpar.Add(SimonDB.CreDbPar("@newscontent", newscontent));
                    lpar.Add(SimonDB.CreDbPar("@sys", "23"));

                    SimonDB.ExecuteNonQuery(@"update ClubApply set IsPass=1,IsRead=1 where id=@id", lpar.ToArray());
                    SimonDB.ExecuteNonQuery(@"delete ClubUser where UserID=(select UserID from ClubApply where id=@id) and ClubID=(select ClubID from ClubApply where id=@id)", lpar.ToArray());
                }

            }

        }
        if (sys == "2")
        {

            if (type == "1")
            {
                string newscontent = "申请加入俱乐部失败：管理拒绝了您加入[" + clubname + "]的申请";

                lpar.Add(SimonDB.CreDbPar("@newscontent", newscontent));
                lpar.Add(SimonDB.CreDbPar("@sys", "22"));

                SimonDB.ExecuteNonQuery(@"update ClubApply set IsPass=2,IsRead=1 where id=@id", lpar.ToArray());
            }

            if (type == "3")
            {
                string newscontent = "申请退出俱乐部失败：管理拒绝了您退出[" + clubname + "]的申请";

                lpar.Add(SimonDB.CreDbPar("@newscontent", newscontent));
                lpar.Add(SimonDB.CreDbPar("@sys", "24"));

                SimonDB.ExecuteNonQuery(@"update ClubApply set IsPass=2,IsRead=1 where id=@id", lpar.ToArray());
            }


        }

        SimonDB.ExecuteNonQuery(@"insert into ClubNews select @newsid,UserID,ClubID,'',@sys,@newscontent,getdate() from ClubApply where id=@id", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 俱乐部房间列表
    protected void GetClubRoomList()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));

        DataTable DT = SimonDB.DataTable(@"select * from FangkaRoomInfo a left join TGameNameInfo b on b.NameID=a.GameNameID where a.CreateClubID=@clubid", new DbParameter[] { SimonDB.CreDbPar("@clubid", clubid) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部没有创建房间信息"));
        }

        int RecordCount = DT.Rows.Count;   //一共几条记录

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RecordNum", DR["RecordNum"].ToString());  //俱乐部唯一标识
            tempdic.Add("RoomID", DR["RoomID"].ToString());  //房间ID
            tempdic.Add("RoomNum", DR["RoomNum"].ToString());  //房间号
            tempdic.Add("Desk", DR["Desk"].ToString());  //桌子号
            tempdic.Add("GameNameID", DR["GameNameID"].ToString());  //游戏ID
            tempdic.Add("ComName", DR["ComName"].ToString());  //游戏名称
            tempdic.Add("PayRule", DR["PayRule"].ToString());  //支付规则
            tempdic.Add("GameRule", DR["GameRule"].ToString());  //游戏规则
            tempdic.Add("JuShuRule", DR["JuShuRule"].ToString());  //局数
            tempdic.Add("PlayersRule", DR["PlayersRule"].ToString());  //玩家人数
            tempdic.Add("Rate", DR["Rate"].ToString());  //倍率
            tempdic.Add("OwnerID", DR["OwnerID"].ToString());  //房主ID
            tempdic.Add("CreateTime", DR["CreateTime"].ToString());  //创建时间
            tempdic.Add("CreateClubID", DR["CreateClubID"].ToString());  //俱乐部ID
            tempdic.Add("Status", DR["Status"].ToString());  //房间状态，0-游戏未开始 1-游戏已结束 2-正在游戏中 3-玩家已解散 4-管理员已解散

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取房间在线玩家
    protected void GetClubRoomOnlineUsers()
    {
        CheckSign();
        string roomnum = Request.Params["roomnum"];
        if (roomnum.Length < 1 || !SimonUtils.IsNum(roomnum))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间号有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@roomnum", roomnum));

        DataTable DT = SimonDB.DataTable(@"select * from TGameLock a left join FangkaRoomInfo c on c.RoomNum=a.RoomNum left join Web_UserWeixin d on d.userid=a.userid left join TUsers e on e.userid=a.userid where a.roomnum=@roomnum and a.roomnum!=0", new DbParameter[] { SimonDB.CreDbPar("@roomnum", roomnum) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间内没有玩家"));
        }

        int RecordCount = DT.Rows.Count;   //一共几条记录

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RoomNum", DR["RoomNum"].ToString());  //房间号
            tempdic.Add("DeskNum", DR["DeskNum"].ToString());  //桌号
            tempdic.Add("DeskStation", DR["DeskStation"].ToString());  //椅子号
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称
            tempdic.Add("HeadIconUrl", DR["HeadIconUrl"].ToString());  //玩家头像


            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取我创建或加入的俱乐部
    protected void GetMyClub()
    {
        CheckSign();
        string userid = Request.Params["userid"];

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        DataTable DT = SimonDB.DataTable(@"select distinct a.*,b.*,c.RoomID,c.GameNameID,ISNULL(d.RoomCard,0) as RoomCard from ClubUser a left join ClubInfo b on b.ClubID=a.ClubID left join TGameRoomInfo c on c.RoomID=b.ServerID left join ClubAgent d on d.Phone=b.AgentPhone where a.UserID=@userid and b.ClubStatus=0", new DbParameter[] { SimonDB.CreDbPar("@userid", userid) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无俱乐部数据"));
        }

        int RecordCount = DT.Rows.Count;


        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("ClubID", DR["ClubID"].ToString());  //俱乐部ID
            tempdic.Add("ClubName", DR["ClubName"].ToString());  //俱乐部名称
            tempdic.Add("IconID", DR["IconID"].ToString());  //俱乐部头像
            tempdic.Add("Notice", DR["Notice"].ToString());  //俱乐部公告
            tempdic.Add("RoomCard", DR["RoomCard"].ToString());  //俱乐部房卡数
            tempdic.Add("ClubDuty", DR["ClubDuty"].ToString());  //俱乐部职称，1会长，2管理，3会员
            tempdic.Add("CreateTime", DR["CreateTime"].ToString());  //创建时间
            tempdic.Add("ClubStatus", DR["ClubStatus"].ToString());  //俱乐部状态，0正常，1不可使用
            tempdic.Add("JoinTime", DR["JoinTime"].ToString());  //加入时间
            tempdic.Add("ServerID", DR["ServerID"].ToString());  //小服务器ID
            tempdic.Add("GameNameID", DR["GameNameID"].ToString());  //游戏ID

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取我创建或加入的俱乐部
    protected void GetMyClub1()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();
        if (userid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@userid", userid));
            lwhere.Add("a.userid=@userid");
        }

        lwhere.Add("b.ClubStatus=0");

        string _countsql = @"select count(1) from ClubUser a left join ClubInfo b on b.ClubID=a.ClubID {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by AddTime desc) as row, * from (ClubUser a left join ClubInfo b on b.ClubID=a.ClubID {0}
                                ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        //DataTable DT = SimonDB.DataTable(@"select * from ClubUser a left join ClubInfo b on b.ClubID=a.ClubID where a.UserID=@userid", new DbParameter[] { SimonDB.CreDbPar("@userid", userid) });
        //if (DT.Rows.Count <= 0)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无俱乐部数据"));
        //}

        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        if (RecordCount > 30)
        {
            RecordCount = 30;
        }
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "5";  //默认每页5条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("ClubID", DR["ClubID"].ToString());  //俱乐部ID
            tempdic.Add("ClubName", DR["ClubName"].ToString());  //俱乐部名称
            tempdic.Add("IconID", DR["IconID"].ToString());  //俱乐部头像
            tempdic.Add("Notice", DR["Notice"].ToString());  //俱乐部公告
            tempdic.Add("ClubDuty", DR["ClubDuty"].ToString());  //俱乐部职称，1会长，2管理，3会员
            tempdic.Add("CreateTime", DR["CreateTime"].ToString());  //创建时间
            tempdic.Add("ClubStatus", DR["ClubStatus"].ToString());  //俱乐部状态，0正常，1不可使用
            tempdic.Add("JoinTime", DR["JoinTime"].ToString());  //加入时间

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 俱乐部战绩
    protected void FangkaRecordByClub()
    {
        CheckSign();
        //string userid = Request.Params["userid"];
        string clubid = Request.Params["clubid"];
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        //if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        //}
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }
        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();
        //lpar.Add(SimonDB.CreDbPar("@userid", userid));
        //if ((int)SimonDB.ExecuteScalar(@"select ClubDuty from  ClubUser where UserID=@userid", lpar.ToArray())==4)
        //{
        //    lwhere.Add("RecordNum in (select RecordNum from FangkaGameRecord where UserID=@userid)");
        //}
        if (clubid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
            lwhere.Add("CreateClubID=@clubid");
        }
        lwhere.Add("Status=1");

        lwhere.Add("RecordNum in (select distinct RecordNum from FangkaGameRecord)");
        string _countsql = @"select count(1) from FangkaRoomInfo {0}";
        //string _listsql = @"select * from (
        //                            select row_number() over (order by AddTime desc) as row, * from (select top 5 a.*,b.GameNameID,b.RoomNum,b.JuShuRule,b.Rate,b.OwnerID from FangkaGameRecord a left join FangkaRoomInfo b on b.RecordNum=a.RecordNum where a.RecordNum in (select RecordNum from FangkaRoomInfo {0}) order by a.AddTime desc) as datatb
        //                        ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";
        //string _listsql = @"select * from (
        //                           select row_number() over (order by CreateTime desc) as row,  * from (select top 30 * 
        //                            from  FangkaRoomInfo {0} order by CreateTime desc) as datatb
        //                        ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        string _listsql = @"select * from (
                                   select top 30 row_number() over (order by b.AddTime desc) as row,  datatb.*,b.AddTime from (select * 
                                    from  FangkaRoomInfo {0}) as datatb left join (select distinct RecordNum,Max(AddTime) as AddTime from FangkaGameRecord group by RecordNum) b on b.RecordNum=datatb.RecordNum
                                ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        if (RecordCount > 30)
        {
            RecordCount = 30;
        }
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "5";  //默认每页5条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RecordNum", DR["RecordNum"].ToString());  //记录编号
            tempdic.Add("GameNameID", DR["GameNameID"].ToString());  //游戏ID
            tempdic.Add("RoomNum", DR["RoomNum"].ToString());  //房间号
            tempdic.Add("JuShuRule", DR["JuShuRule"].ToString());  //游戏总局数
            tempdic.Add("Rate", DR["Rate"].ToString());  //游戏倍率
            tempdic.Add("OwnerID", DR["OwnerID"].ToString());  //房主ID
            tempdic.Add("CreateTime", DR["AddTime"].ToString());  //游戏总结算时间
            //tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            //tempdic.Add("ScoreSum", DR["ScoreSum"].ToString());  //单人总结算分
            //tempdic.Add("IsWinUser", DR["IsWinUser"].ToString());  //是否为大赢家，0否，1是
            //tempdic.Add("IsDissolve", DR["IsDissolve"].ToString());  //是否为发起解散者，0否，1是
            //tempdic.Add("StartTime", DR["StartTime"].ToString());  //游戏开始时间
            //tempdic.Add("AddTime", DR["AddTime"].ToString());  //游戏总结算时间

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取录像文件
    protected void GetVideoDataLog()               //D:\WEB\ttyx_new\VideoDataLog\20190320133855354304_5.bin
    {
        CheckSign();
        string RecordNum = Request.Params["VideoNum"];
        string UserID = Request.Params["UserID"];

        if (RecordNum.Length < 1 || !SimonUtils.IsNum(RecordNum))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "唯一标识有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@RecordNum", RecordNum));
        lpar.Add(SimonDB.CreDbPar("@UserID", UserID));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameSingleRecord where RecordNum=@RecordNum and UserID=@UserID", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无回放记录"));
        }

        DataTable DataDT = SimonDB.DataTable(@"select * from FangkaGameSingleRecord where RecordNum=@RecordNum and UserID=@UserID", lpar.ToArray());

        int RecordCount = DataDT.Rows.Count;   //一共几条记录

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RecordNum", DR["RecordNum"].ToString());  //记录编号
            tempdic.Add("RoundNum", DR["RoundNum"].ToString());  //局号
            tempdic.Add("BackYards", DR["BackYards"].ToString());  //回放码
            tempdic.Add("AddTime", DR["AddTime"].ToString());  //录制时间
            string filePath = AppDomain.CurrentDomain.BaseDirectory + "VideoDataLog\\" + DR["BackYards"].ToString() + ".bin";
            //string filePath = string.Format("http://{0}{1}",Request.Url.Authority, "VideoDataLog\\" + RecordNum + "_" + RoundNum + ".bin") ;

            if (!SimonUtils.FileExists(filePath))
            {
                filePath = "";
            }
            else
            {
                filePath = string.Format("http://{0}{1}", Request.Url.Authority, "/VideoDataLog/" + DR["BackYards"].ToString() + ".bin");
            }
            tempdic.Add("FilePath", filePath);  //下载路径
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));

    }
    #endregion

    #region 获取他人录像文件
    protected void GetUserVideoData()               //D:\WEB\ttyx_new\VideoDataLog\20190320133855354304_5.bin
    {
        CheckSign();
        string BackYards = Request.Params["BackYards"];

        if (BackYards.Length < 1 || !SimonUtils.IsNum(BackYards))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "唯一标识有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@BackYards", BackYards));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameSingleRecord where BackYards=@BackYards", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无回放记录"));
        }

        DataTable DataDT = SimonDB.DataTable(@"select * from FangkaRoomInfo where RecordNum=(select top 1 RecordNum from FangkaGameSingleRecord where BackYards=@BackYards) ", lpar.ToArray());

        string GameNameID = DataDT.Rows[0]["GameNameID"].ToString();   //一共几条记录
        string filePath = AppDomain.CurrentDomain.BaseDirectory + "VideoDataLog\\" + BackYards + ".bin";

        if (!SimonUtils.FileExists(filePath))
        {
            filePath = "";
        }
        else
        {
            filePath = string.Format("http://{0}{1}", Request.Url.Authority, "/VideoDataLog/" + BackYards + ".bin");
        }
        //List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        //foreach (DataRow DR in DataDT.Rows)
        //{
        //    Dictionary<string, string> tempdic = new Dictionary<string, string>();
        //    tempdic.Add("RecordNum", DR["RecordNum"].ToString());  //记录编号
        //    tempdic.Add("GameNameID", DR["GameNameID"].ToString());  //游戏ID
        //    string filePath = AppDomain.CurrentDomain.BaseDirectory + "VideoDataLog\\" + DR["BackYards"].ToString() + ".bin";
        //    //string filePath = string.Format("http://{0}{1}",Request.Url.Authority, "VideoDataLog\\" + RecordNum + "_" + RoundNum + ".bin") ;

        //    if (!SimonUtils.FileExists(filePath))
        //    {
        //        filePath = "";
        //    }
        //    else
        //    {
        //        filePath = string.Format("http://{0}{1}", Request.Url.Authority, "/VideoDataLog/" + DR["BackYards"].ToString() + ".bin");
        //    }
        //    tempdic.Add("FilePath", filePath);  //下载路径
        //    resultslist.Add(tempdic);
        //}

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("gameID", GameNameID.ToString());
        jsondic.Add("filePath", filePath);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));

    }
    #endregion

    #region 修改俱乐部公告
    protected void UpdateClubNotice()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string clubid = Request.Params["clubid"];
        string clubname = Request.Params["clubname"];
        string notice = Request.Params["notice"];

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }
        if (clubname.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部名称不可为空"));
        }
        if (notice.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "公告不可为空"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
        lpar.Add(SimonDB.CreDbPar("@clubname", clubname));
        lpar.Add(SimonDB.CreDbPar("@notice", notice));

        DataTable DataDT = SimonDB.DataTable(@"select * from ClubUser where ClubID=@clubid and UserID=@userid", lpar.ToArray());
        if (DataDT.Rows.Count > 0)
        {
            string duty = DataDT.Rows[0]["ClubDuty"].ToString();
            if (duty == "1" || duty == "2")
            {
                if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubInfo where ClubName=@clubname and ClubID!=@clubid", lpar.ToArray()) > 0)
                {
                    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部名称已存在"));
                }
                else
                {
                    SimonDB.ExecuteNonQuery(@"update ClubInfo set Notice=@notice,ClubName=@clubname where ClubID=@clubid", lpar.ToArray());
                }

            }
            else
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您无权修改俱乐部信息"));
            }
        }
        else
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您无权修改俱乐部信息"));
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 获取对局战绩
    protected void GetUserGameData()
    {
        CheckSign();
        string userid = Request.Params["userid"];

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }

        string gamenameid = "20154500";

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@gamenameid", gamenameid));

        int totalToday = (int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameRecord where userid=@userid and DateDiff(dd,AddTime,getdate())=0", lpar.ToArray());

        int totalYest = (int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameRecord where userid=@userid and DateDiff(dd,AddTime,getdate())=1", lpar.ToArray());

        int winToday1 = (int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameRecord where userid=@userid and ScoreSum>0 and RecordNum in (select RecordNum from FangkaRoomInfo where GameNameID!=@gamenameid) and DateDiff(dd,AddTime,getdate())=0", lpar.ToArray());

        int winYest1 = (int)SimonDB.ExecuteScalar(@"select count(1) from FangkaGameRecord where userid=@userid and ScoreSum>0 and RecordNum in (select RecordNum from FangkaRoomInfo where GameNameID!=@gamenameid) and DateDiff(dd,AddTime,getdate())=1", lpar.ToArray());

        int winToday2 = (int)SimonDB.ExecuteScalar(@"select count(1) from (select a.UserID,a.ScoreSum,a.Score1,b.Extra1 from FangkaGameRecord a left join FangkaRoomInfo b on b.RecordNum=a.RecordNum where b.GameNameID=@gamenameid and DATEDIFF(DD,a.AddTime,GETDATE())=0) as tb where (tb.ScoreSum+tb.Score1-tb.Extra1)>0 and userid=@userid", lpar.ToArray());

        int winYest2 = (int)SimonDB.ExecuteScalar(@"select count(1) from (select a.UserID,a.ScoreSum,a.Score1,b.Extra1 from FangkaGameRecord a left join FangkaRoomInfo b on b.RecordNum=a.RecordNum where b.GameNameID=@gamenameid and DATEDIFF(DD,a.AddTime,GETDATE())=1) as tb where (tb.ScoreSum+tb.Score1-tb.Extra1)>0 and userid=@userid", lpar.ToArray());

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("totalToday", totalToday);
        jsondic.Add("totalYest", totalYest);
        jsondic.Add("winToday", winToday1 + winToday2);
        jsondic.Add("winYest", winYest1 + winYest2);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取俱乐部未读消息数
    protected void ClubMsgCount()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID格式有误"));
        }

        int RecordCount = (int)SimonDB.ExecuteScalar(@"select count(1)  from ClubApply a  where a.ClubID=@clubid and a.IsRead=0 ", new DbParameter[] { SimonDB.CreDbPar("@clubid", clubid) }); ;   //一共几条未读记录

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取俱乐部操作日志
    protected void ClubSysList()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID格式有误"));
        }

        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();
        if (clubid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
            lwhere.Add("a.clubid=@clubid");
        }


        string _countsql = @"select count(1) from ClubNews a {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by AddTime desc) as row, * from (select a.NewsID,a.UserID,ISNULL((select NickName from TUsers where UserID=a.UserID),a.UserID) as UserNickName,a.ClubID,a.OperatorID,ISNULL((select NickName from TUsers where UserID=a.OperatorID),'超级管理员') as OperatorNickName,ISNULL((select ClubDuty from ClubUser where UserID=a.OperatorID and ClubID=a.ClubID),0) as OperatorDuty,NewsType,NewsContent,AddTime from ClubNews a {0})as ta
                                ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }


        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());

        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "5";  //默认每页5条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("ID", DR["row"].ToString());  //ID
            tempdic.Add("NewsID", DR["NewsID"].ToString());  //消息ID
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("UserNickName", DR["UserNickName"].ToString());  //玩家昵称
            tempdic.Add("ClubID", DR["ClubID"].ToString());  //俱乐部ID
            tempdic.Add("OperatorID", DR["OperatorID"].ToString());  //执行人ID
            tempdic.Add("OperatorNickName", DR["OperatorNickName"].ToString());  //执行人昵称
            tempdic.Add("OperatorDuty", DR["OperatorDuty"].ToString());  //执行人职级
            tempdic.Add("NewsType", DR["NewsType"].ToString());  //消息类型：1、升职，2、降职，3、开除，4、值班，5禁止游戏，6解除禁止，7数据清零，8、解散，12、申请创建通过，13、申请创建拒绝，21、申请加入通过，22、申请加入拒绝，23、申请退出通过，24、申请退出拒绝
            tempdic.Add("NewsContent", DR["NewsContent"].ToString());  //附加语
            tempdic.Add("AddTime", DR["AddTime"].ToString());  //添加时间

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 俱乐部开房规则录入
    protected void InsertClubRoomRule()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];
        string payrule = Request.Params["payrule"];
        string jushurule = Request.Params["jushurule"];
        string integralrule = Request.Params["integralrule"];
        string optionsrule = Request.Params["optionsrule"];
        string playrule = Request.Params["playrule"];
        string extrarule = Request.Params["extrarule"];
        string clubrule = Request.Params["clubrule"];

        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID格式有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
        lpar.Add(SimonDB.CreDbPar("@payrule", payrule));
        lpar.Add(SimonDB.CreDbPar("@jushurule", jushurule));
        lpar.Add(SimonDB.CreDbPar("@integralrule", integralrule));
        lpar.Add(SimonDB.CreDbPar("@optionsrule", optionsrule));
        lpar.Add(SimonDB.CreDbPar("@playrule", playrule));
        lpar.Add(SimonDB.CreDbPar("@extrarule", extrarule));
        lpar.Add(SimonDB.CreDbPar("@clubrule", clubrule));

        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubRoomRule where ClubID=@clubid", lpar.ToArray()) > 0)
        {
            SimonDB.ExecuteNonQuery(@"update ClubRoomRule set PayRule=@payrule,JushuRule=@jushurule,IntegralRule=@integralrule,OptionsRule=@optionsrule,PlayRule=@playrule,ExtraRule=@extrarule,ClubRule=@clubrule,UpdateTime=getdate() where ClubID=@clubid", lpar.ToArray());
        }
        else
        {
            SimonDB.ExecuteNonQuery(@"insert into ClubRoomRule values(@clubid,@payrule,@jushurule,@integralrule,@optionsrule,@playrule,@extrarule,@clubrule,getdate())", lpar.ToArray());
        }


        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 获取俱乐部开放配置规则
    protected void GetClubRoomRule()
    {
        CheckSign();
        string clubid = Request.Params["clubid"];

        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID格式有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));

        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubRoomRule where ClubID=@clubid", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部暂无历史开房配置规则"));
        }

        DataTable DataDT = SimonDB.DataTable(@"select * from ClubRoomRule where ClubID=@clubid", lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("ClubID", DR["ClubID"].ToString());  //俱乐部ID
            tempdic.Add("PayRule", DR["PayRule"].ToString());  //支付规则
            tempdic.Add("JushuRule", DR["JushuRule"].ToString());  //局数规则
            tempdic.Add("IntegralRule", DR["IntegralRule"].ToString());  //开局积分
            tempdic.Add("OptionsRule", DR["OptionsRule"].ToString());  //选项
            tempdic.Add("PlayRule", DR["PlayRule"].ToString());  //玩法规则
            tempdic.Add("ExtraRule", DR["ExtraRule"].ToString());  //进园子
            tempdic.Add("ClubRule", DR["ClubRule"].ToString());  //俱乐部规则
            tempdic.Add("UpdateTime", DR["UpdateTime"].ToString());  //最后一次开放配置时间

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);
        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 上传图片
    protected void UploadImg()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        //string img= Request.Params["imgbase"];



        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID格式有误"));
        }

        //if (img.Length < 1)
        //{
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "图片格式有误"));
        //}

        System.Web.HttpFileCollection _file = System.Web.HttpContext.Current.Request.Files;

        //文件大小
        long size = _file[0].ContentLength;
        //文件类型
        string type = _file[0].ContentType;
        //文件名
        string name = _file[0].FileName;
        //文件格式
        string _tp = System.IO.Path.GetExtension(name);
        //获取文件流
        System.IO.Stream stream = _file[0].InputStream;
        //保存文件



        //string fileName = DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString();//年月
        //string fileName = String.Format("{0}.jpg",img);
        string ImageFilePath = "/Upload/UserImg" + "/";
        if (System.IO.Directory.Exists(HttpContext.Current.Server.MapPath(ImageFilePath)) == false)//如果不存在就创建文件夹
        {
            System.IO.Directory.CreateDirectory(HttpContext.Current.Server.MapPath(ImageFilePath));
        }
        string fileName = userid + System.DateTime.Now.ToString("yyyyHHddHHmmss") + ".jpg";
        string ImagePath = HttpContext.Current.Server.MapPath(ImageFilePath) + fileName;//定义图片名称
        // File.WriteAllBytes(ImagePath + ".png", bt); //保存图片到服务器，然后获取路径  
        //string ImagePath = "";
        _file[0].SaveAs(ImagePath);
        string result = "http://" + Request.Url.Authority + "/Upload/UserImg/" + fileName;//获取保存后的路径


        //byte[] bt = Convert.FromBase64String(img);
        //string fileName = DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString();//年月
        //string ImageFilePath = "/Upload/UserImg" + "/" + fileName;
        //if (System.IO.Directory.Exists(HttpContext.Current.Server.MapPath(ImageFilePath)) == false)//如果不存在就创建文件夹
        //{
        //    System.IO.Directory.CreateDirectory(HttpContext.Current.Server.MapPath(ImageFilePath));
        //}
        //string ImagePath = HttpContext.Current.Server.MapPath(ImageFilePath) + "/" +userid+ System.DateTime.Now.ToString("yyyyHHddHHmmss");//定义图片名称
        //File.WriteAllBytes(ImagePath + ".png", bt); //保存图片到服务器，然后获取路径  
        //string result = ImagePath + ".png";//获取保存后的路径

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@imgUrl", result));

        if ((int)SimonDB.ExecuteScalar(@"select count(1) from Web_UsersImg where UserID=@userid", lpar.ToArray()) > 0)
        {
            SimonDB.ExecuteNonQuery(@"update Web_UsersImg set ImgUrl=@imgUrl where UserID=@userid", lpar.ToArray());
        }
        else
        {
            SimonDB.ExecuteNonQuery(@"insert into Web_UsersImg values(@userid,@imgUrl,getdate())", lpar.ToArray());
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 获取图片
    protected void GetUserImg()
    {
        CheckSign();
        string userid = Request.Params["userid"];

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from Web_UsersImg where UserID=@userid", lpar.ToArray()) <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无图片记录"));
        }

        DataTable DataDT = SimonDB.DataTable(@"select * from Web_UsersImg where UserID=@userid ", lpar.ToArray());

        string ImgUrl = DataDT.Rows[0]["ImgUrl"].ToString();

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("imgurl", ImgUrl);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取用户是否被封禁
    protected void GetClubUserStatus()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string clubid = Request.Params["clubid"];
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));



        DataTable DataDT = SimonDB.DataTable(@"select * from ClubUser where UserID=@userid and ClubID=@clubid", lpar.ToArray());

        string status = "0";

        if (DataDT.Rows.Count > 0)
        {
            status = DataDT.Rows[0]["Status"].ToString();
        }
        else
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "不存在该玩家信息"));
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("status", status);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 每天给用户送金币
    protected void GiveUserCoin()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string gold = Request.Params["gold"];

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (gold.Length < 1 || !SimonUtils.IsNum(gold))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "金币格式有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@gold", gold));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from Web_MoneyChangeLog where ChangeType=27 and UserID=@userid and DATEDIFF(DD,DateTime,GETDATE())=0", lpar.ToArray()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您今日已经参与过一次了"));
        }
        else
        {
            SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog  select a.UserID,a.UserName,b.WalletMoney,@gold,27,0,GETDATE(),'每日分享赠送' from TUsers a left join TUserInfo b on b.UserID=a.UserID where a.userid=@userid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@gold where UserID=@userid", lpar.ToArray());
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 玩家绑定手机号
    protected void BindPhone()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string phone = Request.Params["phone"];

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (phone.Length < 1 || !SimonUtils.IsNum(phone))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "电话格式有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@phone", phone));
        lpar.Add(SimonDB.CreDbPar("@RecordNum", DateTime.Now.ToString("yyyyMMddHHmmss") + userid));
        DataTable UserDT = SimonDB.DataTable(@"select * from TUserInfo where userid=@userid", lpar.ToArray());
        if (UserDT.Rows.Count > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@StartNum", UserDT.Rows[0]["RoomCard"].ToString()));
        }
        else
        {
            lpar.Add(SimonDB.CreDbPar("@StartNum", 0));
        }

        //如果用户是第一次绑定手机号，赠送房卡5张
        lpar.Add(SimonDB.CreDbPar("@fangka", 5));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from Web_Users where UserID=@userid and (Phone is null or Phone='')", lpar.ToArray()) > 0)
        {
            SimonDB.ExecuteNonQuery(@"update TUserInfo set RoomCard=RoomCard+@fangka where UserID=@userid", lpar.ToArray());
            SimonDB.ExecuteNonQuery(@"insert into FangkaRecord values(@userid, @RecordNum, 1, @StartNum, @fangka, 3, GETDATE(), '首绑手机号赠送')", lpar.ToArray());
        }
        SimonDB.ExecuteNonQuery(@"update Web_Users set Phone=@phone where UserID=@userid", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 复制微信号
    protected void CopyVX()
    {
        CheckSign();

        string VX_Account = "";
        DataTable DataDT = SimonDB.DataTable(@"select top 1 * from ClubVXAccount order by AddTime desc ");
        if (DataDT.Rows.Count > 0)
        {
            VX_Account = DataDT.Rows[0]["VXaccount"].ToString();
        }
        else
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "系统未设定微信号"));
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["vx_account"] = VX_Account;

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 个人信息战绩接口（进园子）

    protected void UserGameData()
    {
        CheckSign();

        string date = Request.Params["date"];//1今天，2昨天，3前天，4一周，5十五天，6三十天
        string jinyuanzi = Request.Params["jinyuanzi"];//0全部，1进园子
        string userid = Request.Params["userid"];  //0全部
        string clubid = Request.Params["clubid"];  //俱乐部ID,0全部
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        if (date.Length < 1 || !SimonUtils.IsNum(date))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "日期选项有误"));
        }
        if (jinyuanzi.Length < 1 || !SimonUtils.IsNum(jinyuanzi))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "模式选项有误"));
        }
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }
        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();

        if (clubid != "0")
        {
            lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
            lwhere.Add("RecordNum in (select RecordNum from FangkaRoomInfo where CreateClubID=@clubid)");
        }

        if (userid != "0")
        {
            lpar.Add(SimonDB.CreDbPar("@userid", userid));
            lwhere.Add("userid=@userid");

            if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where UserID=@userid and ClubID=@clubid and ResetTime is not null", lpar.ToArray()) > 0)
            {
                DataTable UserData = SimonDB.DataTable(@"select * from ClubUser where UserID=@userid and ClubID=@clubid", lpar.ToArray());
                string resettime = UserData.Rows[0]["ResetTime"].ToString();
                lpar.Add(SimonDB.CreDbPar("@ResetTime", resettime));
                lwhere.Add("AddTime>@ResetTime");
            }
        }

        if (jinyuanzi == "1")
        {
            lwhere.Add("ScoreSum=0");
        }

        if (date == "1")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())=0");
        }
        if (date == "2")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())=1");
        }
        if (date == "3")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())=2");
        }
        if (date == "4")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())<=7");
        }
        if (date == "5")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())<=15");
        }
        if (date == "6")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())<=30");
        }

        string _countsql = @"select count(1) from (
                                    select UserID, CONVERT(decimal(19,2),SUM(convert(decimal(19,2),(ScoreSum-100+Score1)*Rate)/100)) as SumScore,SUM(Score1) as Score1,COUNT(RecordNum) as SumNum from (select x.*,y.Rate from FangkaGameRecord x left join FangkaRoomInfo y on y.RecordNum=x.RecordNum) datedt {0}  group by UserID
                                ) a left join
                                (
                                select UserID,COUNT(RecordNum)as WinNum from FangkaGameRecord {0} and IsWinUser=1 group by UserID
                                ) b on b.UserID=a.UserID";
        string _listsql = @"select * from (
                                    select row_number() over (order by SumScore desc) as row,  a.*,b.WinNum,c.NickName  from (
                                      select UserID, CONVERT(decimal(19,2),SUM(convert(decimal(19,2),(ScoreSum-100+Score1)*Rate)/100)) as SumScore,SUM(Score1) as Score1,COUNT(RecordNum) as SumNum from (select x.*,y.Rate from FangkaGameRecord x left join FangkaRoomInfo y on y.RecordNum=x.RecordNum) datedt  {0}  group by UserID
                                    ) a left join
                                    (
                                    select UserID,COUNT(RecordNum)as WinNum from FangkaGameRecord  {0} and IsWinUser=1 group by UserID
                                    ) b on b.UserID=a.UserID
                                    left join (select UserID,NickName from TUsers) c on c.UserID=a.UserID)  as datetb
                                     where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        if (RecordCount > 30)
        {
            RecordCount = 30;
        }
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "5";  //默认每页5条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
            tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称
            tempdic.Add("SumNum", DR["SumNum"].ToString());  //总局数
            tempdic.Add("SumScore", DR["SumScore"].ToString());  //总分数    麒陵麻将
            // tempdic.Add("SumScore", DR["SumScore"].ToString());  //总分数
            tempdic.Add("WinNum", DR["WinNum"].ToString());  //大赢家总数

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }

    //protected void UserGameData()
    //{
    //    CheckSign();

    //    string date = Request.Params["date"];//1今天，2昨天，3前天，4一周，5十五天，6三十天
    //    string jinyuanzi = Request.Params["jinyuanzi"];//0全部，1进园子
    //    string userid = Request.Params["userid"];  //0全部
    //    string clubid = Request.Params["clubid"];  //俱乐部ID,0全部
    //    string pageindex = SimonUtils.Qnum("pageindex");  //页码
    //    string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
    //    if (date.Length < 1 || !SimonUtils.IsNum(date))
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "日期选项有误"));
    //    }
    //    if (jinyuanzi.Length < 1 || !SimonUtils.IsNum(jinyuanzi))
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "模式选项有误"));
    //    }
    //    if (userid.Length < 1 || !SimonUtils.IsNum(userid))
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
    //    }
    //    if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
    //    }
    //    if (pageindex.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
    //    }
    //    if (pagesize.Length < 1)
    //    {
    //        SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
    //    }

    //    List<DbParameter> lpar = new List<DbParameter>();
    //    List<string> lwhere = new List<string>();

    //    if (clubid != "0")
    //    {
    //        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
    //        lwhere.Add("RecordNum in (select RecordNum from FangkaRoomInfo where CreateClubID=@clubid)");
    //    }

    //    if (userid != "0")
    //    {
    //        lpar.Add(SimonDB.CreDbPar("@userid", userid));
    //        lwhere.Add("userid=@userid");

    //        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where UserID=@userid and ClubID=@clubid and ResetTime is not null", lpar.ToArray()) > 0)
    //        {
    //            DataTable UserData = SimonDB.DataTable(@"select * from ClubUser where UserID=@userid and ClubID=@clubid", lpar.ToArray());
    //            string resettime = UserData.Rows[0]["ResetTime"].ToString();
    //            lpar.Add(SimonDB.CreDbPar("@ResetTime", resettime));
    //            lwhere.Add("AddTime>@ResetTime");
    //        }
    //    }

    //    if (jinyuanzi == "1")
    //    {
    //        lwhere.Add("ScoreSum=0");
    //    }

    //    if (date == "1")
    //    {
    //        lwhere.Add("DateDiff(dd,AddTime,getdate())=0");
    //    }
    //    if (date == "2")
    //    {
    //        lwhere.Add("DateDiff(dd,AddTime,getdate())=1");
    //    }
    //    if (date == "3")
    //    {
    //        lwhere.Add("DateDiff(dd,AddTime,getdate())=2");
    //    }
    //    if (date == "4")
    //    {
    //        lwhere.Add("DateDiff(dd,AddTime,getdate())<=7");
    //    }
    //    if (date == "5")
    //    {
    //        lwhere.Add("DateDiff(dd,AddTime,getdate())<=15");
    //    }
    //    if (date == "6")
    //    {
    //        lwhere.Add("DateDiff(dd,AddTime,getdate())<=30");
    //    }

    //    string _countsql = @"select count(1) from (
    //                            select UserID,SUM(ScoreSum) as SumScore,COUNT(RecordNum) as SumNum, SUM(Score1) as Score1 from FangkaGameRecord {0}  group by UserID
    //                            ) a left join
    //                            (
    //                            select UserID,COUNT(RecordNum)as WinNum from FangkaGameRecord {0} and IsWinUser=1 group by UserID
    //                            ) b on b.UserID=a.UserID";
    //    string _listsql = @"select * from (
    //                                select row_number() over (order by SumScore desc) as row,  * from (
    //                                select a.UserID,c.NickName,a.SumScore,a.Score1,a.SumNum,ISNULL(b.WinNum,0) as WinNum from (
    //                                select UserID,SUM(ScoreSum) as SumScore,SUM(Score1) as Score1,COUNT(RecordNum) as SumNum from FangkaGameRecord {0}  group by UserID
    //                                ) a left join
    //                                (
    //                                select UserID,COUNT(RecordNum)as WinNum from FangkaGameRecord  {0} and IsWinUser=1 group by UserID
    //                                ) b on b.UserID=a.UserID
    //                                left join (select UserID,NickName from TUsers) c on c.UserID=a.UserID)  as datetb
    //                                ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

    //    if (lwhere.Count > 0)
    //    {
    //        string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
    //        _countsql = string.Format(_countsql, _sqlwhere);
    //        _listsql = string.Format(_listsql, _sqlwhere);
    //    }
    //    else
    //    {
    //        _countsql = string.Format(_countsql, string.Empty);
    //        _listsql = string.Format(_listsql, string.Empty);
    //    }

    //    int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
    //    if (RecordCount > 30)
    //    {
    //        RecordCount = 30;
    //    }
    //    int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
    //    if (pageindex == "0") pageindex = "1";  //默认第1页
    //    if (pagesize == "0") pagesize = "5";  //默认每页5条
    //    if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

    //    lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
    //    lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

    //    DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());

    //    List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
    //    foreach (DataRow DR in ListDT.Rows)
    //    {
    //        Dictionary<string, string> tempdic = new Dictionary<string, string>();
    //        tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
    //        tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称
    //        tempdic.Add("SumNum", DR["SumNum"].ToString());  //总局数
    //        tempdic.Add("SumScore", (Convert.ToInt32(DR["SumScore"])+ Convert.ToInt32(DR["Score1"]) - Convert.ToInt32(DR["SumNum"])*100).ToString());  //总分数    麒陵麻将
    //       // tempdic.Add("SumScore", DR["SumScore"].ToString());  //总分数
    //        tempdic.Add("WinNum", DR["WinNum"].ToString());  //大赢家总数

    //        resultslist.Add(tempdic);
    //    }

    //    Dictionary<string, object> jsondic = new Dictionary<string, object>();
    //    jsondic.Add("code", "1");
    //    jsondic.Add("msg", "success");
    //    jsondic.Add("recordcount", RecordCount.ToString());
    //    jsondic.Add("totalpage", TotalPage.ToString());
    //    jsondic.Add("pagesize", pagesize);
    //    jsondic.Add("pageindex", pageindex);
    //    jsondic.Add("results", resultslist);

    //    SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    //}

    protected void UserGameData1()
    {
        CheckSign();

        string date = Request.Params["date"];//1今天，2昨天，3前天，4一周，5十五天，6三十天
        string jinyuanzi = Request.Params["jinyuanzi"];//0全部，1进园子
        string userid = Request.Params["userid"];  //0全部
        string clubid = Request.Params["clubid"];  //俱乐部ID,0全部
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        if (date.Length < 1 || !SimonUtils.IsNum(date))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "日期选项有误"));
        }
        if (jinyuanzi.Length < 1 || !SimonUtils.IsNum(jinyuanzi))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "模式选项有误"));
        }
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }
        if (pageindex.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "页码错误"));
        }
        if (pagesize.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "每页记录条数错误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();

        if (clubid != "0")
        {
            lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
            lwhere.Add("RecordNum in (select RecordNum from FangkaRoomInfo where CreateClubID=@clubid)");
        }

        if (userid != "0")
        {
            lpar.Add(SimonDB.CreDbPar("@userid", userid));
            lwhere.Add("userid=@userid");

            if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where UserID=@userid and ClubID=@clubid and ResetTime is not null", lpar.ToArray()) > 0)
            {
                DataTable UserData = SimonDB.DataTable(@"select * from ClubUser where UserID=@userid and ClubID=@clubid", lpar.ToArray());
                string resettime = UserData.Rows[0]["ResetTime"].ToString();
                lpar.Add(SimonDB.CreDbPar("@ResetTime", resettime));
                lwhere.Add("AddTime>@ResetTime");
            }
        }

        if (jinyuanzi == "1")
        {
            lwhere.Add("ScoreSum=0");
        }

        if (date == "1")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())=0");
        }
        if (date == "2")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())=1");
        }
        if (date == "3")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())=2");
        }
        if (date == "4")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())<=7");
        }
        if (date == "5")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())<=15");
        }
        if (date == "6")
        {
            lwhere.Add("DateDiff(dd,AddTime,getdate())<=30");
        }

        int RecordCount = 0;
        int TotalPage = 0;
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        if (userid != "0")
        {


            string _listsql = @"select * from (
                                    select a.UserID,c.NickName,a.SumScore,a.SumNum,ISNULL(b.WinNum,0) as WinNum from (
                                    select UserID,SUM(ScoreSum) as SumScore,COUNT(RecordNum) as SumNum from FangkaGameRecord {0}  group by UserID
                                    ) a left join
                                    (
                                    select UserID,COUNT(RecordNum)as WinNum from FangkaGameRecord  {0} and IsWinUser=1 group by UserID
                                    ) b on b.UserID=a.UserID
                                    left join (select UserID,NickName from TUsers) c on c.UserID=a.UserID)  as datetb";

            if (lwhere.Count > 0)
            {
                string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
                _listsql = string.Format(_listsql, _sqlwhere);
            }
            else
            {
                _listsql = string.Format(_listsql, string.Empty);
            }

            DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
            if (ListDT.Rows.Count > 0)
            {
                RecordCount = 1;
                TotalPage = 1;

                Dictionary<string, string> tempdic = new Dictionary<string, string>();
                tempdic.Add("UserID", ListDT.Rows[0]["UserID"].ToString());  //玩家ID
                tempdic.Add("NickName", ListDT.Rows[0]["NickName"].ToString());  //玩家昵称
                tempdic.Add("SumNum", ListDT.Rows[0]["SumNum"].ToString());  //总局数
                //tempdic.Add("SumScore", (Convert.ToInt32(DR["SumScore"])-Convert.ToInt32(DR["SumNum"])*100).ToString());  //总分数
                tempdic.Add("SumScore", ListDT.Rows[0]["SumScore"].ToString());  //总分数
                tempdic.Add("WinNum", ListDT.Rows[0]["WinNum"].ToString());  //大赢家总数

                resultslist.Add(tempdic);
            }

        }
        else
        {
            string _countsql = @"select count(1) from (select distinct UserID from FangkaGameRecord {0}) b ";
            string _listsql = @"select * from(
                                        select row_number() over (order by UserID desc) as row,* from(
			                            select a.UserID,b.NickName from
                                        (
                                        select distinct UserID from FangkaGameRecord {0}
                                        )a left join
                                        (
                                        select UserID,NickName from TUsers 
                                        ) b on b.UserID=a.UserID) as tb) as tbnew
                                        where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";
            if (lwhere.Count > 0)
            {
                string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
                _countsql = string.Format(_countsql, _sqlwhere);
                _listsql = string.Format(_listsql, _sqlwhere);
            }
            else
            {
                _countsql = string.Format(_countsql, string.Empty);
                _listsql = string.Format(_listsql, string.Empty);
            }

            RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
            if (RecordCount > 30)
            {
                RecordCount = 30;
            }
            TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
            if (pageindex == "0") pageindex = "1";  //默认第1页
            if (pagesize == "0") pagesize = "5";  //默认每页5条
            if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

            lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
            lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

            DataTable ListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
            foreach (DataRow DR in ListDT.Rows)
            {
                
                TotalPage = 1;

                Dictionary<string, string> tempdic = new Dictionary<string, string>();
                tempdic.Add("UserID", DR["UserID"].ToString());  //玩家ID
                tempdic.Add("NickName", DR["NickName"].ToString());  //玩家昵称

                lpar.Add(SimonDB.CreDbPar("@userid", DR["UserID"].ToString()));
                lwhere.Add("userid=@userid");

                if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUser where UserID=@userid and ClubID=@clubid and ResetTime is not null", lpar.ToArray()) > 0)
                {
                    DataTable UserData = SimonDB.DataTable(@"select * from ClubUser where UserID=@userid and ClubID=@clubid", lpar.ToArray());
                    string resettime = UserData.Rows[0]["ResetTime"].ToString();
                    lpar.Add(SimonDB.CreDbPar("@ResetTime", resettime));
                    lwhere.Add("AddTime>@ResetTime");
                }
                DataTable UserDataDT = SimonDB.DataTable(@"select a.UserID,a.SumScore,a.SumNum,ISNULL(b.WinNum,0) as WinNum from (
                            select UserID,SUM(ScoreSum) as SumScore,COUNT(RecordNum) as SumNum from FangkaGameRecord {0}  group by UserID ) 
                            a left join(
                            select UserID,COUNT(RecordNum)as WinNum from FangkaGameRecord  {0} and IsWinUser=1 group by UserID)
                             b on b.UserID=a.UserID", lpar.ToArray());
                tempdic.Add("SumNum", UserDataDT.Rows[0]["SumNum"].ToString());  //总局数
                tempdic.Add("SumScore", (Convert.ToInt32(UserDataDT.Rows[0]["SumScore"]) - Convert.ToInt32(UserDataDT.Rows[0]["SumNum"]) * 100).ToString());  //总分数
                //tempdic.Add("SumScore", ListDT.Rows[0]["SumScore"].ToString());  //总分数
                tempdic.Add("WinNum", UserDataDT.Rows[0]["WinNum"].ToString());  //大赢家总数
                

                resultslist.Add(tempdic);
            }
        }
        
        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 设置俱乐部权限
    protected void SetClubLimit()
    {

        CheckSign();
        string userid = Request.Params["userid"];
        string clubid = Request.Params["clubid"];
        string isAgreeJoin = Request.Params["isAgreeJoin"];
        string isKickOut = Request.Params["isKickOut"];
        string isOpenRoom = Request.Params["isOpenRoom"];
        string isDissolve = Request.Params["isDissolve"];
        string isEmpty = Request.Params["isEmpty"];
        string isForbid = Request.Params["isForbid"];
        string isCheck = Request.Params["isCheck"];

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }
        if (isAgreeJoin.Length < 1 || !SimonUtils.IsNum(isAgreeJoin))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "参数1有误"));
        }
        if (isKickOut.Length < 1 || !SimonUtils.IsNum(isKickOut))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "参数2有误"));
        }
        if (isOpenRoom.Length < 1 || !SimonUtils.IsNum(isOpenRoom))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "参数3有误"));
        }
        if (isDissolve.Length < 1 || !SimonUtils.IsNum(isDissolve))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "参数4有误"));
        }
        if (isEmpty.Length < 1 || !SimonUtils.IsNum(isEmpty))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "参数5有误"));
        }
        if (isForbid.Length < 1 || !SimonUtils.IsNum(isForbid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "参数6有误"));
        }
        if (isCheck.Length < 1 || !SimonUtils.IsNum(isCheck))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "参数7有误"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
        lpar.Add(SimonDB.CreDbPar("@isAgreeJoin", isAgreeJoin));
        lpar.Add(SimonDB.CreDbPar("@isKickOut", isKickOut));
        lpar.Add(SimonDB.CreDbPar("@isOpenRoom", isOpenRoom));
        lpar.Add(SimonDB.CreDbPar("@isDissolve", isDissolve));
        lpar.Add(SimonDB.CreDbPar("@isEmpty", isEmpty));
        lpar.Add(SimonDB.CreDbPar("@isForbid", isForbid));
        lpar.Add(SimonDB.CreDbPar("@isCheck", isCheck));
        if ((int)SimonDB.ExecuteScalar(@"select count(1) from ClubUserLimits where userid=@userid and clubid=@clubid", lpar.ToArray()) > 0)
        {
            SimonDB.ExecuteNonQuery(@"update ClubUserLimits set  isAgreeJoin=@isAgreeJoin,isKickOut=@isKickOut,isOpenRoom=@isOpenRoom,isDissolve=@isDissolve,isEmpty=@isEmpty,isForbid=@isForbid,isCheck=@isCheck where userid=@userid and clubid=@clubid", lpar.ToArray());
        }
        else
        {
            SimonDB.ExecuteNonQuery(@"insert into ClubUserLimits values (@userid,@clubid,@isAgreeJoin,@isKickOut,@isOpenRoom,@isDissolve,@isEmpty,@isForbid,@isCheck)", lpar.ToArray());
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 查看玩家详细权限
    protected void CheckUserLimits()
    {
        CheckSign();
        string userid = Request.Params["userid"];
        string clubid = Request.Params["clubid"];

        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家ID有误"));
        }
        if (clubid.Length < 1 || !SimonUtils.IsNum(clubid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "俱乐部ID有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@clubid", clubid));
        DataTable DataDT = SimonDB.DataTable(@"select * from ClubUserLimits where userid=@userid and clubid=@clubid", lpar.ToArray());

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DataDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("userid", DR["userid"].ToString());  //玩家ID
            tempdic.Add("clubid", DR["clubid"].ToString());  //玩家昵称
            tempdic.Add("isAgreeJoin", DR["isAgreeJoin"].ToString());  //通过申请
            tempdic.Add("isKickOut", DR["isKickOut"].ToString());  //踢出
            tempdic.Add("isOpenRoom", DR["isOpenRoom"].ToString());  //开房
            tempdic.Add("isDissolve", DR["isDissolve"].ToString());  //解散
            tempdic.Add("isEmpty", DR["isEmpty"].ToString());  //清空数据
            tempdic.Add("isForbid", DR["isForbid"].ToString());  //禁止游戏
            tempdic.Add("isCheck", DR["isCheck"].ToString());  //查看战绩

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion
}