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
using System.Text;
using System.Data;
using System.Data.Common;

using Microsoft.Security.Application;
using LitJson;
using Simon.Common; 

public partial class Api_Admin : System.Web.UI.Page
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

    #region 管理员登录检查(辅助方法)
    /// <summary>
    /// 管理员登录检查
    /// </summary>
    /// <returns></returns>
    private bool CheckAdminLogin(string token, out string outadminuserid)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            outadminuserid = "0";
            return false;
        }
        string _detoken = string.Empty;
        try
        {
            _detoken = SimonDES.Decrypt(token);
        }
        catch (Exception)
        {
            outadminuserid = "0";
            return false;
        }
        string[] _detokenarr = _detoken.Split('|');
        if (_detokenarr.Length != 2)
        {
            outadminuserid = "0";
            return false;
        }
        string _aid = _detokenarr[0];
        string _logindt = _detokenarr[1];
        int _cookieexp = CurrSite.CookieExp;
        if (_cookieexp == 0) _cookieexp = 720; //默认时效时间720分钟(12小时)
        if (SimonUtils.IsNum(_aid) && SimonUtils.IsStringDate(_logindt) && DateTime.Parse(_logindt).AddMinutes(_cookieexp) >= DateTime.Now)
        {
            DbParameter[] parms = { SimonDB.CreDbPar("@id", _aid), SimonDB.CreDbPar("@token", token) };
            if (((int)SimonDB.ExecuteScalar(@"select count(*) from adminuser where id=@id and token=@token", parms)) > 0)
            {
                outadminuserid = _aid;
                return true;
            }
        }
        outadminuserid = "0";
        return false;
    }
    #endregion

    #region 管理端API验签检查(辅助方法)
    /// <summary>
    /// 管理端API验签检查(辅助方法)
    /// </summary>
    private void CheckAdminSign()
    {
        string t = SimonUtils.Qnum("t");  //unix时间戳 (10位数字)
        string sign = SimonUtils.Q("sign");  //签名

        if (CurrSite.CheckAdminSign && t.Length != 10)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "时间戳错误"));
        }
        if (CurrSite.CheckAdminSign && CurrSite.ApiCallTimeOut(t))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请求超时"));
        }
        if (CurrSite.CheckAdminSign && !CurrSite.AdminVerifySign(sign, t))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "签名错误"));
        }
    }
    #endregion


    #region 管理员登陆
    protected void AdminLogin()
    {
        CheckAdminSign();
        string aname = Request.Params["aname"];  //用户名
        string apwd = Request.Params["apwd"];  //密码(明文)

        if (string.IsNullOrWhiteSpace(aname))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写用户名"));
        }
        if (aname.Length > 50)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户名最多50个字符"));
        }
        if (string.IsNullOrWhiteSpace(apwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写密码"));
        }

        DbParameter[] auparms = { SimonDB.CreDbPar("@user_name", aname), SimonDB.CreDbPar("@user_password", SimonUtils.EnCodeMD5(apwd)) };
        DataTable AUDT = SimonDB.DataTable(@"select * from adminuser where user_name=@user_name and user_password=@user_password", auparms);
        if (AUDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户名或密码错误"));
        }

        DataRow AUDR = AUDT.Rows[0];
        //生成登陆token并加密,格式为 管理员id|登录时间
        string token = SimonDES.Encrypt(string.Format("{0}|{1}", AUDR["id"].ToString(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

        //更新上次登录时间、上次登录IP、登录次数、token
        DbParameter[] updateparms = { 
                SimonDB.CreDbPar("@lastdate", DateTime.Now.ToString()),
                SimonDB.CreDbPar("@lastip", SimonUtils.GetUserIp()),
                SimonDB.CreDbPar("@token", token),
                SimonDB.CreDbPar("@id", AUDR["id"].ToString())};
        SimonDB.ExecuteNonQuery(@"update adminuser set lastdate=@lastdate,lastip=@lastip,logincount=logincount+1,token=@token where id=@id", updateparms);

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["token"] = token;
        jd["results"]["aid"] = AUDR["id"].ToString();
        jd["results"]["aname"] = AUDR["user_name"].ToString();
        jd["results"]["lastdateip"] = string.Format("{0}|{1}", AUDR["lastdate"].ToString(), AUDR["lastip"].ToString());
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 管理员退出
    protected void AdminLogout()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }

        //设置token失效
        DbParameter[] updateparms = { SimonDB.CreDbPar("@token", token) };
        SimonDB.ExecuteNonQuery(@"update adminuser set token=null where token=@token", updateparms);

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = "null";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 管理员修改密码
    protected void AdminEdit()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string apwd = Request.Params["apwd"];  //原密码(明文)
        string newapwd = Request.Params["newapwd"];  //新密码(明文)

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (string.IsNullOrWhiteSpace(apwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写原密码"));
        }
        if (string.IsNullOrWhiteSpace(newapwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写新密码"));
        }

        DbParameter[] auparms = { SimonDB.CreDbPar("@id", adminuserid), SimonDB.CreDbPar("@user_password", SimonUtils.EnCodeMD5(apwd)) };
        DataTable AUDT = SimonDB.DataTable(@"select * from adminuser where id=@id and user_password=@user_password", auparms);
        if (AUDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "原密码错误"));
        }

        string aid = AUDT.Rows[0]["id"].ToString();
        DbParameter[] updateparms = {
                SimonDB.CreDbPar("@user_password", SimonUtils.EnCodeMD5(newapwd)),
                SimonDB.CreDbPar("@id", aid)};

        SimonDB.ExecuteNonQuery(@"update adminuser set user_password=@user_password where id=@id", updateparms);

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["aname"] = AUDT.Rows[0]["user_name"].ToString();
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 管理员详情
    protected void AdminDetails()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }

        DbParameter[] auparms = { SimonDB.CreDbPar("@id", adminuserid) };
        DataTable AUDT = SimonDB.DataTable(@"select * from adminuser where id=@id", auparms);
        if (AUDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "管理员不存在"));
        }

        DataRow AUDR = AUDT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["aname"] = AUDR["user_name"].ToString();
        jd["results"]["realname"] = AUDR["realname"].ToString();
        jd["results"]["mobile"] = AUDR["mobile"].ToString();
        jd["results"]["regip"] = AUDR["regip"].ToString();
        jd["results"]["lastdate"] = AUDR["lastdate"].ToString();
        jd["results"]["lastip"] = AUDR["lastip"].ToString();
        jd["results"]["logincount"] = AUDR["logincount"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    } 
    #endregion


    #region 获取游戏公告(管理端使用)
    protected void GetGameNotice()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string noticetype = SimonUtils.Qnum("noticetype");  //公告类型（0普通公告，1兑奖公告）

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
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

    #region 保存游戏公告
    protected void EditGameNotice()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string noticetype = SimonUtils.Qnum("noticetype");  //公告类型
        string noticecon = Request.Params["noticecon"]; //公告内容

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (noticetype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "公告类型错误"));
        }
        if (string.IsNullOrWhiteSpace(noticecon) || noticecon.Length > 1000)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "公告内容1-1000个字符"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@noticetype", noticetype));
        lpar.Add(SimonDB.CreDbPar("@noticecon", noticecon));
        lpar.Add(SimonDB.CreDbPar("@adddate", DateTime.Now.ToString()));

        if ((int)SimonDB.ExecuteScalar(@"select count(*) from gamenotice where noticetype=@noticetype", lpar.ToArray()) > 0)
        {
            SimonDB.ExecuteNonQuery(@"update gamenotice set noticetype=@noticetype,noticecon=@noticecon,adddate=@adddate where noticetype=@noticetype", lpar.ToArray());
        }
        else
        {
            SimonDB.ExecuteNonQuery(@"insert into gamenotice (noticetype,noticecon,adddate) values (@noticetype,@noticecon,@adddate)", lpar.ToArray());
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["noticetype"] = noticetype;
        jd["results"]["noticecon"] = noticecon;
        SimonUtils.RespWNC(jd.ToJson());
    } 
    #endregion

    #region 推荐人列表(分页)
    protected void RecUserList()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string kw = Request.Params["kw"];  //(非必选)查询关键字 模糊匹配 RecUserID,RealName,Mobile

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (!string.IsNullOrWhiteSpace(kw))
        {
            lpar.Add(SimonDB.CreDbPar("@kw", kw + "%"));
            lwhere.Add("(recuserid like @kw or realname like @kw or mobile like @kw)");
        }

        string _countsql = @"select count(1) from RecUser {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by adddate desc) as row, * from RecUser {0}
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

        DataTable RecUserDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in RecUserDT.Rows)
        {
            DataTable _MoneyChangeTotalDT = SimonDB.DataTable(@"select coalesce(sum(ChangeMoney),0) as all_total,
                                                                             coalesce(sum(case when ChangeMoney > 0 then ChangeMoney else 0 end),0) as increase_total,
                                                                             coalesce(sum(case when ChangeMoney < 0 then ChangeMoney else 0 end),0) as reduce_total
                                                                      from Web_MoneyChangeLog as a inner join TUsers as b on a.UserID=b.UserID 
                                                                      where b.recuserid=@recuserid", new DbParameter[] {
                SimonDB.CreDbPar("@recuserid", DR["RecUserID"].ToString())
            });
            int user_total = (int)SimonDB.ExecuteScalar(@"select count(1) from TUsers where recuserid=@recuserid", new DbParameter[] {
                SimonDB.CreDbPar("@recuserid", DR["RecUserID"].ToString())
            });

            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("recuserid", DR["RecUserID"].ToString());
            tempdic.Add("parentrecuserid", DR["ParentRecUserID"].ToString());
            tempdic.Add("realname", DR["RealName"].ToString());
            tempdic.Add("mobile", DR["Mobile"].ToString());
            tempdic.Add("address", DR["Address"].ToString());
            tempdic.Add("allowrecharge", DR["Allowrecharge"].ToString());
            tempdic.Add("coinbalance", DR["Coinbalance"].ToString());
            tempdic.Add("adddate", DR["AddDate"].ToString());
            tempdic.Add("shareprofittype",DR["ShareProfitType"].ToString());
            tempdic.Add("shareprofitproportion", DR["ShareProfitProportion"].ToString());
            tempdic.Add("user_total", user_total.ToString());
            tempdic.Add("moneychangetotal", _MoneyChangeTotalDT.Rows[0]["all_total"].ToString());
            tempdic.Add("increase_total", _MoneyChangeTotalDT.Rows[0]["increase_total"].ToString());
            tempdic.Add("reduce_total", _MoneyChangeTotalDT.Rows[0]["reduce_total"].ToString());
            
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

    #region 推荐人详情
    protected void RecUserDetails()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string recuserid = SimonUtils.Qnum("recuserid");  //推荐人ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID错误"));
        }

        DataTable RecUserDT = SimonDB.DataTable(@"select * from recuser where recuserid=@recuserid", new DbParameter[] { SimonDB.CreDbPar("@recuserid", recuserid) });
        if (RecUserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人不存在"));
        }

        DataRow RecUserDR = RecUserDT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["recuserid"] = RecUserDR["RecUserID"].ToString();
        jd["results"]["realname"] = RecUserDR["RealName"].ToString();
        jd["results"]["mobile"] = RecUserDR["Mobile"].ToString();
        jd["results"]["address"] = RecUserDR["Address"].ToString();
        jd["results"]["allowrecharge"] = RecUserDR["Allowrecharge"].ToString();
        jd["results"]["coinbalance"] = RecUserDR["Coinbalance"].ToString();
        jd["results"]["adddate"] = RecUserDR["AddDate"].ToString();
        jd["results"]["shareprofittype"] = RecUserDR["ShareProfitType"].ToString();
        jd["results"]["shareprofitproportion"] = RecUserDR["ShareProfitProportion"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 推荐人分销比例详情
    protected void RecProportion()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }

        DataTable RecProportionDT = SimonDB.DataTable(@"select * from RecLevelProportion");
        if (RecProportionDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无数据，请前去系统设置"));
        }

        DataRow RecProportionDR = RecProportionDT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "获取数据成功";
        jd["results"] = new JsonData();
        jd["results"]["level1proportion"] = RecProportionDR["FristLevelProportion"].ToString();
        jd["results"]["level2proportion"] = RecProportionDR["SecondLevelProportion"].ToString();
        jd["results"]["level3proportion"] = RecProportionDR["ThirdLevelProportion"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 推荐人分销比例修改 (格式例如：0.10)
    protected void RecProportionSet()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string level1proportion = Request.Params["level1proportion"];
        string level2proportion = Request.Params["level2proportion"];
        string level3proportion = Request.Params["level3proportion"];

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (level1proportion.Length < 1|| !SimonUtils.IsDecimal2(level1proportion)|| decimal.Parse(level1proportion) < 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "1级比例设置格式有误"));
        }
        if (level2proportion.Length < 1 || !SimonUtils.IsDecimal2(level2proportion) || decimal.Parse(level3proportion) < 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "2级比例设置格式有误"));
        }
        if (level3proportion.Length < 1 || !SimonUtils.IsDecimal2(level3proportion) || decimal.Parse(level3proportion) < 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "3级比例设置格式有误"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@level1proportion", level1proportion));
        lpar.Add(SimonDB.CreDbPar("@level2proportion", level2proportion));
        lpar.Add(SimonDB.CreDbPar("@level3proportion", level3proportion));

        SimonDB.ExecuteNonQuery(@"update RecLevelProportion set FristLevelProportion=@level1proportion,SecondLevelProportion=@level2proportion,ThirdLevelProportion=@level3proportion where id=1", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "修改比例成功";
        jd["results"] = "null";
        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    #region 添加,编辑推荐人
    protected void EditRecUser()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string act = SimonUtils.Q("act");  //操作动作 act=add添加,act=edit修改
        string recuserid = SimonUtils.Qnum("recuserid");  //推荐人ID
        string parentrecuserid = SimonUtils.Qnum("parentrecuserid"); //上级推荐人（可为空，为空则为系统配置）
        string recuserpwd = Request.Params["recuserpwd"];  //推荐人密码(明文)
        string realname = Request.Params["realname"];  //推荐人姓名 中文、英文或数字混合
        string mobile = SimonUtils.Qnum("mobile");  //推荐人手机号
        string address = Request.Params["address"];  //推荐人地址
        string allowrecharge = SimonUtils.Qnum("allowrecharge"); //是否允许给自己名下用户充值： 0(默认值)不允许  1允许
        string shareprofittype = SimonUtils.Qnum("ShareProfitType");//推荐人分润类型
        string shareprofitproportion = Request.Params["ShareProfitProportion"];//推荐人分润比例


        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人ID(数字类型)"));
        }
        if (act.Equals("add", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(recuserpwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "添加推荐人时请填写推荐人登录密码"));
        }
        if (string.IsNullOrWhiteSpace(realname) || realname.Length > 50)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人姓名(1-50个字符)"));
        }
        if (mobile.Length != 11 || !SimonUtils.IsNum(mobile))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人手机号(11位数字)"));
        }
        if (!string.IsNullOrWhiteSpace(address) && address.Length > 200)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人地址(最多200个字符)"));
        }
        if (allowrecharge.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人是否允许给自己名下用户充值(数字类型)"));
        }
        if (shareprofittype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请选择推荐人分润类型"));
        }
        if (shareprofittype != "1" && shareprofittype != "2")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人分润不包含该类型"));
        }
        if (shareprofitproportion.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人分润比例"));
        }
        if (!SimonUtils.IsDecimal2(shareprofitproportion))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分润比例应为小数类型(小数点后最多可保留3位)"));
        }
        if (decimal.Parse(shareprofitproportion) < 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分润比例须大于0"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));
        lpar.Add(SimonDB.CreDbPar("@parentrecuserid", parentrecuserid));
        lpar.Add(SimonDB.CreDbPar("@realname", realname));
        lpar.Add(SimonDB.CreDbPar("@mobile", mobile));
        lpar.Add(SimonDB.CreDbPar("@address", address));
        lpar.Add(SimonDB.CreDbPar("@adddate", DateTime.Now.ToString()));
        lpar.Add(SimonDB.CreDbPar("@allowrecharge", allowrecharge));
        lpar.Add(SimonDB.CreDbPar("@shareprofittype", shareprofittype));
        lpar.Add(SimonDB.CreDbPar("@shareprofitproportion", shareprofitproportion));

        if (act.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
                if ((int)SimonDB.ExecuteScalar(@"select count(*) from recuser where recuserid=@recuserid", lpar.ToArray()) > 0)
                {
                    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID已存在"));
                }

                SimonDB.ExecuteNonQuery(@"insert into recuser (recuserid,parentrecuserid,realname,mobile,address,adddate,allowrecharge,ShareProfitType,ShareProfitProportion) values(@recuserid,@parentrecuserid,@realname,@mobile,@address,@adddate,@allowrecharge,@shareprofittype,@shareprofitproportion)", lpar.ToArray());

                SimonDB.ExecuteNonQuery(@"update recuser set recuserpwd=@recuserpwd where recuserid=@recuserid", new DbParameter[] 
                {
                    SimonDB.CreDbPar("@recuserpwd", SimonUtils.EnCodeMD5(recuserpwd)),
                    SimonDB.CreDbPar("@recuserid", recuserid)
                });

                //向推广员金币表添加一条推广员基本信息
                SimonDB.ExecuteNonQuery(@"insert into recusergold (recuserid) values (@recuserid)",lpar.ToArray());    

                JsonData jd = new JsonData();
                jd["code"] = "1";
                jd["msg"] = "success";
                jd["results"] = new JsonData();
                jd["results"]["recuserid"] = recuserid;
                SimonUtils.RespWNC(jd.ToJson());

        }

        if (act.Equals("edit", StringComparison.OrdinalIgnoreCase))
        {
            SimonDB.ExecuteNonQuery(@"update recuser set realname=@realname,mobile=@mobile,address=@address,adddate=@adddate,allowrecharge=@allowrecharge,shareprofittype=@shareprofittype,shareprofitproportion=@shareprofitproportion where recuserid=@recuserid", lpar.ToArray());

            if (!string.IsNullOrWhiteSpace(recuserpwd))
            {
                SimonDB.ExecuteNonQuery(@"update recuser set recuserpwd=@recuserpwd where recuserid=@recuserid", new DbParameter[] {
                    SimonDB.CreDbPar("@recuserpwd", SimonUtils.EnCodeMD5(recuserpwd)),
                    SimonDB.CreDbPar("@recuserid", recuserid)
                });
            }

            JsonData jd = new JsonData();
            jd["code"] = "1";
            jd["msg"] = "success";
            jd["results"] = new JsonData();
            jd["results"]["recuserid"] = recuserid;
            SimonUtils.RespWNC(jd.ToJson());
        }
    }
    #endregion

    #region 删除推荐人
    protected void DelRecUser()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string recuserid = SimonUtils.Qnum("recuserid");  //推荐人ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID(数字类型)错误"));
        }

        SimonDB.ExecuteNonQuery(@"delete from recuser where recuserid=@recuserid", new DbParameter[] { SimonDB.CreDbPar("@recuserid", recuserid) });

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["recuserid"] = recuserid;
        SimonUtils.RespWNC(jd.ToJson());
    } 
    #endregion

    #region 游戏用户列表(分页)
    protected void UserList()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string kw = Request.Params["kw"];  //(非必选)查询关键字 模糊匹配 UserID,UserName
        string recuserid = SimonUtils.Qnum("recuserid");  //(非必选)推荐人ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (!string.IsNullOrWhiteSpace(kw))
        {
            lpar.Add(SimonDB.CreDbPar("@kw", kw + "%"));
            lwhere.Add("(a.UserID like @kw or a.UserName like @kw)");
        }
        if (recuserid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));
            lwhere.Add("a.recuserid=@recuserid");
        }

        lwhere.Add("isrobot=0"); //过滤掉机器人

        string _countsql = @"select count(1) from TUsers as a inner join Web_Users as b on a.UserID=b.UserID inner join TUserInfo as c on a.UserID=c.UserID {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by c.WalletMoney desc, b.RegisterTM desc) as row,
                                    a.*, b.RegisterTM,b.RegisterIP,c.WalletMoney
                                    from TUsers as a inner join Web_Users as b on a.UserID=b.UserID 
                                    inner join TUserInfo as c on a.UserID=c.UserID {0}
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

        DataTable UserDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in UserDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("userid", DR["UserID"].ToString());
            tempdic.Add("username", DR["UserName"].ToString());
            tempdic.Add("nickname", DR["NickName"].ToString());
            tempdic.Add("recuserid", DR["RecUserID"].ToString());
            tempdic.Add("sex", DR["Sex"].ToString());
            tempdic.Add("disabled", DR["Disabled"].ToString());
            tempdic.Add("walletmoney", DR["WalletMoney"].ToString());
            tempdic.Add("registertm", DR["RegisterTM"].ToString());
            tempdic.Add("registerip", DR["RegisterIP"].ToString());

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

    #region 游戏用户详情
    protected void UserDetails()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string userid = SimonUtils.Qnum("userid");  //游戏用户ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (userid.Length < 1 || !SimonUtils.IsNum(userid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写游戏用户ID(数字类型)"));
        }

        DataTable UserDT = SimonDB.DataTable(@"select a.*, b.RegisterTM,b.RegisterIP, c.WalletMoney
                                               from TUsers as a inner join Web_Users as b on a.UserID=b.UserID inner join TUserInfo as c on a.UserID=c.UserID
                                               where a.userid=@userid", new DbParameter[] { SimonDB.CreDbPar("@userid", userid) });
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏用户不存在"));
        }

        DataRow UserDR = UserDT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["userid"] = UserDR["UserID"].ToString();
        jd["results"]["username"] = UserDR["UserName"].ToString();
        jd["results"]["nickname"] = UserDR["NickName"].ToString();
        jd["results"]["recuserid"] = UserDR["RecUserID"].ToString();
        jd["results"]["sex"] = UserDR["Sex"].ToString();
        jd["results"]["disabled"] = UserDR["Disabled"].ToString();
        jd["results"]["walletmoney"] = UserDR["WalletMoney"].ToString();
        jd["results"]["registertm"] = UserDR["RegisterTM"].ToString();
        jd["results"]["registerip"] = UserDR["RegisterIP"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    } 
    #endregion

    #region 获取所有用户金币总额
    protected void GetAllUserMoney()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }

        long allusermoney = (long)SimonDB.ExecuteScalar(@"select coalesce(sum(walletmoney),0) from TUserInfo as a
                                                          inner join TUsers as b on a.userid=b.userid where isrobot=0");

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["allusermoney"] = allusermoney.ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 修改用户金币数(充值、赠送)
    protected void ChangeUserMoney()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string userid = SimonUtils.Qnum("userid");  //游戏用户ID
        string changetype = SimonUtils.Qnum("changetype");  //修改金币类型 101.充值 102.赠送 103.扣除
        string changemoney = Request.Params["changemoney"];  //充值、扣除金币数(例如： 充值10000金币，扣除时为负值 -10000金)
        //20180110新增变化总值（totalchangemoney[变化总值],ishasgive[是否有赠送]）
        string totalchangemoney = Request.Params["totalchangemoney"];  //变化总值
        string ishasgive= Request.Params["ishasgive"];   //是否包含赠送：0没有，1有

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写游戏用户ID(数字类型)"));
        }
        if (changetype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写修改金币类型"));
        }
        if (changetype != "101" && changetype != "102" && changetype != "103")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "修改金币类型只能为(数字类型)：101.管理员修改用户金币数-充值  102.管理员修改用户金币数-赠送  103.管理员修改用户金币数-扣除"));
        }
        if (string.IsNullOrWhiteSpace(changemoney))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写充值或扣除金币数(整数类型,包含负值)"));
        }
        if (!SimonUtils.IsInt(changemoney))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值或扣除金币数为整数类型(包含负值)"));
        }
        if ((changetype == "101" || changetype == "102") && long.Parse(changemoney) < 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值，赠送金币时,金币数需为正数"));
        }
        if (changetype == "103" && long.Parse(changemoney) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "扣除金币时,金币数需为负数"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));

        DataTable UserDT = SimonDB.DataTable(@"select a.*, b.WalletMoney from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", lpar.ToArray());
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏用户不存在"));
        }

        //判断用户是否在游戏中
        if ((int)SimonDB.ExecuteScalar(@"select count(*) from TWLoginRecord where userid=@userid", lpar.ToArray()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        }
        if ((int)SimonDB.ExecuteScalar(@"select count(*) from TZLoginRecord where userid=@userid", lpar.ToArray()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户在线,用户离线后才能充值或扣除金币"));
        }
        //20180110增加配分状态查询
       if((int)SimonDB.ExecuteScalar(@"select count(*) from Web_UserPartitionCardSatus where userid=@userid and UserLockSatus=1", lpar.ToArray()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该用户配分锁定状态未解除"));
        }

        DataRow UserDR = UserDT.Rows[0];
        if (long.Parse(changemoney) < 0 && -long.Parse(changemoney) > long.Parse(UserDR["WalletMoney"].ToString()))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "扣除金币数不能大于用户钱包金币数"));
        }

        string Remark = "";
        switch (changetype)
        {
            case "101":
                Remark = "管理员修改用户金币数-充值";
                break;
            case "102":
                Remark = "管理员修改用户金币数-赠送";
                break;
            case "103":
                Remark = "管理员修改用户金币数-扣除";
                break;
        }

        //增加配分卡赠送分值,对应锁定值
        //int[] giveGold = { 50,100,68,88,188,150,200,300,400,500,600,700,800,1000,1500,1800,2000,1100,1300,1600};
        //int PartitioStatus = 0;
        //switch (totalchangemoney)
        //{
        //    case "50":
        //        PartitioStatus = 5;
        //        break;
        //    case "68":
        //        PartitioStatus = 6;
        //        break;
        //    case "88":
        //        PartitioStatus = 8;
        //        break;
        //    case "100":
        //        PartitioStatus = 10;
        //        break;
        //    case "150":
        //        PartitioStatus = 15;
        //        break;
        //    case "188":
        //        PartitioStatus = 18;
        //        break;
        //    case "200":
        //        PartitioStatus = 20;
        //        break;
        //    case "300":
        //        PartitioStatus = 30;
        //        break;
        //    case "400":
        //        PartitioStatus = 40;
        //        break;
        //    case "500":
        //        PartitioStatus = 50;
        //        break;
        //    case "600":
        //        PartitioStatus = 60;
        //        break;
        //    case "700":
        //        PartitioStatus = 70;
        //        break;
        //    case "800":
        //        PartitioStatus = 80;
        //        break;
        //    case "1000":
        //        PartitioStatus = 100;
        //        break;
        //    case "1100":
        //        PartitioStatus = 110;
        //        break;
        //    case "1300":
        //        PartitioStatus = 130;
        //        break;
        //    case "1500":
        //        PartitioStatus = 150;
        //        break;
        //    case "1600":
        //        PartitioStatus = 160;
        //        break;
        //    case "1800":
        //        PartitioStatus = 180;
        //        break;
        //    case "2000":
        //        PartitioStatus = 200;
        //        break;
        //}
        //lpar.Add(SimonDB.CreDbPar("@partitiostatus", PartitioStatus));

        int PartitioStatus = 0;
        if (Convert.ToInt32(totalchangemoney) >=0)
        {
            PartitioStatus = Convert.ToInt32(totalchangemoney);
        }
       lpar.Add(SimonDB.CreDbPar("@partitiostatus", PartitioStatus));
        //更新玩家金币数
        lpar.Add(SimonDB.CreDbPar("@changemoney", changemoney));
        SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@changemoney where userid=@userid", lpar.ToArray());
        if (changetype == "101")
        {
            if ((int)SimonDB.ExecuteScalar(@"select count(*) from Web_UserPartitionCardSatus where userid=@userid", lpar.ToArray()) > 0)
            {
                SimonDB.ExecuteNonQuery(@"update Web_UserPartitionCardSatus set PartitionSatus=@partitiostatus,UserLockSatus=0 where UserID=@userid", lpar.ToArray());
            }
            else
            {
                SimonDB.ExecuteNonQuery(@"insert into Web_UserPartitionCardSatus (UserID,PartitionSatus,UserLockSatus) values (@userid,@partitiostatus,0)", lpar.ToArray());
            }
        }
        if (changetype == "102")
        {
            // SimonDB.ExecuteNonQuery(@"update TUserInfo set GiveMoney=GiveMoney+@changemoney where userid=@userid", lpar.ToArray());
            //if (giveGold.Contains(Convert.ToInt32(totalchangemoney))&&ishasgive=="1")
            if ((int)SimonDB.ExecuteScalar(@"select count(*) from Web_UserPartitionCardSatus where userid=@userid", lpar.ToArray()) > 0)
            {
                SimonDB.ExecuteNonQuery(@"update Web_UserPartitionCardSatus set UserLockSatus=1 where UserID=@userid", lpar.ToArray());
            }
            else
            {
                SimonDB.ExecuteNonQuery(@"insert into Web_UserPartitionCardSatus (UserID,PartitionSatus,UserLockSatus) values (@userid,@partitiostatus,1)", lpar.ToArray());
            }
         }
        //写入金币变化日志
        lpar.Add(SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@ChangeType", changetype));
        lpar.Add(SimonDB.CreDbPar("@Remark", Remark));
        SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,OpuserType,DateTime,Remark)
                                                          Values (@UserID,@UserName,@StartMoney,@ChangeMoney,@ChangeType,1,getdate(),@Remark)", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["userid"] = UserDR["UserID"].ToString();
        jd["results"]["username"] = UserDR["UserName"].ToString();
        jd["results"]["startmoney"] = UserDR["WalletMoney"].ToString();
        jd["results"]["changemoney"] = changemoney;

        SimonUtils.RespWNC(jd.ToJson());
    } 
    #endregion

    #region 金币变化日志(分页)
    protected void MoneyChangeLog()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string changetype = SimonUtils.Qnum("changetype");  //(非必选)金币变化类型: 为空. 筛选所有的 101.充值 102.赠送 103.扣除
        string opusertype = SimonUtils.Qnum("opusertype");  //(非必选)操作人类型： 为空. 筛选所有的 1.管理员操作的  2.推荐人操作的
        string userid = SimonUtils.Qnum("userid");  //(非必选)游戏用户ID
        string recuserid = SimonUtils.Qnum("recuserid");   //(非必选)推荐人用户ID
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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
        if (recuserid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));
            lwhere.Add("b.recuserid=@recuserid");
        }
        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("a.DateTime >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("a.DateTime <= @enddt");
        }
        if (changetype != "")
        {
            lpar.Add(SimonDB.CreDbPar("@changetype", changetype));
            lwhere.Add("a.changetype=@changetype");
        }
        if(opusertype != "")
        {
            lpar.Add(SimonDB.CreDbPar("@opusertype", opusertype));
            lwhere.Add("a.opusertype=@opusertype");
        }

        lwhere.Add("isrobot=0"); //过滤掉机器人

        string _moneychangetotalsql = @"select coalesce(sum(ChangeMoney),0) as all_total,
                                               coalesce(sum(case when ChangeMoney > 0 then ChangeMoney else 0 end),0) as increase_total,
                                               coalesce(sum(case when ChangeMoney < 0 then ChangeMoney else 0 end),0) as reduce_total
                                        from Web_MoneyChangeLog as a inner join TUsers as b on a.UserID=b.UserID {0}";
        string _countsql = @"select count(1) from Web_MoneyChangeLog as a inner join TUsers as b on a.UserID=b.UserID {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by DateTime desc) as row,
                                    b.userid,b.username,b.nickname,b.recuserid,a.startmoney,a.changemoney,a.changetype,a.opusertype,a.datetime,a.remark
                                    from Web_MoneyChangeLog as a inner join TUsers as b on a.UserID=b.UserID {0}
                                ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _moneychangetotalsql = string.Format(_moneychangetotalsql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _moneychangetotalsql = string.Format(_moneychangetotalsql, string.Empty);
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable MoneyChangeTotalDT = SimonDB.DataTable(_moneychangetotalsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable MoneyChangeLogDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in MoneyChangeLogDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("userid", DR["UserID"].ToString());
            tempdic.Add("username", DR["UserName"].ToString());
            tempdic.Add("nickname", DR["NickName"].ToString());
            tempdic.Add("recuserid", DR["RecUserID"].ToString());
            tempdic.Add("startmoney", DR["StartMoney"].ToString());
            tempdic.Add("changemoney", DR["ChangeMoney"].ToString());
            tempdic.Add("changetype", DR["ChangeType"].ToString());
            tempdic.Add("opusertype", DR["OpuserType"].ToString());
            tempdic.Add("datetime", DR["DateTime"].ToString());
            tempdic.Add("remark", DR["Remark"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("moneychangetotal", MoneyChangeTotalDT.Rows[0]["all_total"].ToString());
        jsondic.Add("increase_total", MoneyChangeTotalDT.Rows[0]["increase_total"].ToString());
        jsondic.Add("reduce_total", MoneyChangeTotalDT.Rows[0]["reduce_total"].ToString());
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    } 
    #endregion

    #region 兑奖(下分)订单管理(分页)
    protected void CashPrizeOrderMGT()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string state = SimonUtils.Qnum("state");  //(非必选)订单状态 0 未处理，1 已处理，2 已拒绝
        string kw = Request.Params["kw"];  //(非必选)查询关键字 模糊匹配 userid,username

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (!string.IsNullOrWhiteSpace(kw))
        {
            lpar.Add(SimonDB.CreDbPar("@kw", kw + "%"));
            lwhere.Add("(userid like @kw or username like @kw)");
        }
        if (state.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@state", state));
            lwhere.Add("state=@state");
        }

        string _countsql = @"select count(1) from cashprizeorder {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by state asc, adddate desc) as row, * from cashprizeorder {0}
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
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 修改兑奖(下分)订单状态
    protected void EditCashPrizeOrderState()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string oid = SimonUtils.Qnum("oid");  //订单id
        string state = SimonUtils.Qnum("state");  //订单状态 0 未处理，1 已处理，2 已拒绝

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (oid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单ID错误(数字类型)"));
        }
        if (state.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单状态错误(数字类型)"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@oid", oid));
        lpar.Add(SimonDB.CreDbPar("@state", state));

        DataTable DT = SimonDB.DataTable(@"select * from cashprizeorder where id=@oid", lpar.ToArray());
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单不存在"));
        }

        SimonDB.ExecuteNonQuery(@"update cashprizeorder set state=@state where id=@oid", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["oid"] = oid;
        jd["results"]["state"] = state;
        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    #region 获取分享链接和二维码URL(管理端使用)
    protected void GetShareLinkQRCode()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }

        DataTable ShareLinkQRCodeDT = SimonDB.DataTable(@"select * from ShareLinkQRCode");
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
        jd["results"]["sharecon"] = ShareLinkQRCodeDR["sharecon"].ToString();
        jd["results"]["link"] = ShareLinkQRCodeDR["sharelink"].ToString();
        jd["results"]["qrcodeurl"] = shareqrcode;
        jd["results"]["sharepic"] = sharepic;
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 分享链接和二维码图片管理（已过时API）
    protected void ShareLinkQRCodeMGT()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string sharetype = SimonUtils.Qnum("sharetype");  //分享类型
        string sharetitle = Request.Params["sharetitle"];  //分享标题
        string sharecon = Request.Params["sharecon"];  //分享内容
        string link = Request.Params["link"];  //分享链接
        string qrcodelink = Request.Params["qrcodelink"];  //二维码链接
        string sharepic = Request.Params["sharepic"];  //分享图片链接

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (sharetype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享类型错误"));
        }
        if (string.IsNullOrWhiteSpace(sharetitle))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享标题错误"));
        }
        if (string.IsNullOrWhiteSpace(sharecon))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享内容错误"));
        }
        if (string.IsNullOrWhiteSpace(link))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享链接错误"));
        }
        if (string.IsNullOrWhiteSpace(qrcodelink))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "二维码链接错误"));
        }
        if (string.IsNullOrWhiteSpace(sharepic))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享图片链接错误"));
        }

        DbParameter[] parms = new DbParameter[] {
            SimonDB.CreDbPar("@sharetype", sharetype),
            SimonDB.CreDbPar("@sharetitle", sharetitle),
            SimonDB.CreDbPar("@sharecon", sharecon),
            SimonDB.CreDbPar("@sharelink", link),
            SimonDB.CreDbPar("@shareqrcode", qrcodelink),
            SimonDB.CreDbPar("@sharepic", sharepic)
        };
        SimonDB.ExecuteNonQuery(@"update ShareLinkQRCode set sharetype=@sharetype,sharetitle=@sharetitle,sharecon=@sharecon,sharelink=@sharelink,shareqrcode=@shareqrcode,sharepic=@sharepic where isenable=1", parms);

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 分享链接列表
    protected void ShareLinkList()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string sharetype = SimonUtils.Qnum("sharetype");  //分享类型 1.图片分享、2.文字分享、3.二维码分享、4.链接分享  此参数为空显示所有 

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();

        if (sharetype.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@sharetype", sharetype));
            lwhere.Add("sharetype=@sharetype");
        }
        string _listsql = @"select * from ShareLinkQRCode {0} order by isenable desc, id desc";
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
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("id", DR["id"].ToString());
            tempdic.Add("sharetype", DR["sharetype"].ToString());
            tempdic.Add("sharetitle", DR["sharetitle"].ToString());
            tempdic.Add("sharedes", DR["sharedes"].ToString());
            tempdic.Add("sharecon", DR["sharecon"].ToString());
            tempdic.Add("sharelink", DR["sharelink"].ToString());
            tempdic.Add("shareqrcode", DR["shareqrcode"].ToString());
            tempdic.Add("sharepic", DR["sharepic"].ToString());
            tempdic.Add("isenable", DR["isenable"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 分享链接详情
    protected void ShareLinkDetails()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string id = SimonUtils.Qnum("id");  //分享链接ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (id.Length < 1 || !SimonUtils.IsNum(id))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享链接ID错误"));
        }

        DataTable DT = SimonDB.DataTable(@"select * from ShareLinkQRCode where id=@id", new DbParameter[] { SimonDB.CreDbPar("@id", id) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享链接不存在"));
        }

        DataRow DR = DT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["id"] = DR["id"].ToString();
        jd["results"]["sharetype"] = DR["sharetype"].ToString();
        jd["results"]["sharetitle"] = DR["sharetitle"].ToString();
        jd["results"]["sharedes"] = DR["sharedes"].ToString();
        jd["results"]["sharecon"] = DR["sharecon"].ToString();
        jd["results"]["sharelink"] = DR["sharelink"].ToString();
        jd["results"]["shareqrcode"] = DR["shareqrcode"].ToString();
        jd["results"]["sharepic"] = DR["sharepic"].ToString();
        jd["results"]["isenable"] = DR["isenable"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 添加,编辑分享链接
    protected void EditShareLink()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string act = SimonUtils.Q("act");  //操作动作 act=add添加,act=edit修改
        string id = SimonUtils.Qnum("id");  //分享链接ID，添加时无需提交该参数，编辑时必须提交该参数
        string sharetype = SimonUtils.Qnum("sharetype");  //分享类型 1.图片分享、2.文字分享、3.二维码分享、4.链接分享
        string sharetitle = Request.Params["sharetitle"];  //分享标题
        string sharedes = Request.Params["sharedes"];  //分享描述
        string sharecon = Request.Params["sharecon"];  //分享内容
        string sharelink = Request.Params["sharelink"];  //分享链接
        string shareqrcode = Request.Params["shareqrcode"];  //分享二维码
        string sharepic = Request.Params["sharepic"];  //分享图片
        string isenable = SimonUtils.Qnum("isenable");  //分享是否启用（0禁用、1启用 同时只有一条分享为启用状态）

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (act.Equals("edit", StringComparison.OrdinalIgnoreCase) && (id.Length < 1 || !SimonUtils.IsNum(id)))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写分享链接ID(数字类型)"));
        }
        if (sharetype.Length < 1 || sharetype.Equals("0"))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写分享类型(数字类型) 1.图片分享、2.文字分享、3.二维码分享、4.链接分享 "));
        }
        if (!string.IsNullOrWhiteSpace(sharetitle) && sharetitle.Length > 200)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享标题(0-200个字符)"));
        }
        if (!string.IsNullOrWhiteSpace(sharedes) && sharedes.Length > 300)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享描述(0-300个字符)"));
        }
        if (!string.IsNullOrWhiteSpace(sharecon) && sharecon.Length > 500)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分享内容(0-500个字符)"));
        }
        if (isenable.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写分享是否启用（0禁用、1启用 同时只有一条分享为启用状态）"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@sharetype", sharetype));
        lpar.Add(SimonDB.CreDbPar("@sharetitle", sharetitle ?? ""));
        lpar.Add(SimonDB.CreDbPar("@sharedes", sharedes ?? ""));
        lpar.Add(SimonDB.CreDbPar("@sharecon", sharecon ?? ""));
        lpar.Add(SimonDB.CreDbPar("@sharelink", sharelink ?? ""));
        lpar.Add(SimonDB.CreDbPar("@shareqrcode", shareqrcode ?? ""));
        lpar.Add(SimonDB.CreDbPar("@sharepic", sharepic ?? ""));
        lpar.Add(SimonDB.CreDbPar("@isenable", isenable));

        if (act.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            int resultid = SimonDB.Insert(@"insert into ShareLinkQRCode (sharetype,sharetitle,sharedes,sharecon,sharelink,shareqrcode,sharepic,isenable)
                                                               values (@sharetype,@sharetitle,@sharedes,@sharecon,@sharelink,@shareqrcode,@sharepic,@isenable)", lpar.ToArray());

            //同时只有一条分享为启用状态
            if (isenable == "1")
            {
                SimonDB.ExecuteNonQuery(@"update ShareLinkQRCode set isenable=0 where id<>@id", new DbParameter[] { SimonDB.CreDbPar("@id", resultid) });
            }

            JsonData jd = new JsonData();
            jd["code"] = "1";
            jd["msg"] = "success";
            jd["results"] = new JsonData();
            jd["results"]["id"] = resultid.ToString();
            SimonUtils.RespWNC(jd.ToJson());
        }

        if (act.Equals("edit", StringComparison.OrdinalIgnoreCase))
        {
            lpar.Add(SimonDB.CreDbPar("@id", id));

            SimonDB.ExecuteNonQuery(@"update ShareLinkQRCode set sharetype=@sharetype,sharetitle=@sharetitle,sharedes=@sharedes,sharecon=@sharecon,sharelink=@sharelink,shareqrcode=@shareqrcode,sharepic=@sharepic,isenable=@isenable where id=@id", lpar.ToArray());

            //同时只有一条分享为启用状态
            if (isenable == "1")
            {
                SimonDB.ExecuteNonQuery(@"update ShareLinkQRCode set isenable=0 where id<>@id", lpar.ToArray());
            }

            JsonData jd = new JsonData();
            jd["code"] = "1";
            jd["msg"] = "success";
            jd["results"] = new JsonData();
            jd["results"]["id"] = id;
            SimonUtils.RespWNC(jd.ToJson());
        }
    }
    #endregion

    #region 启用/禁用分享链接
    protected void ShareLinkEnable()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string id = SimonUtils.Qnum("id");  //分享链接ID
        string isenable = SimonUtils.Qnum("isenable");  //分享是否启用（0禁用、1启用 同时只有一条分享为启用状态）

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (isenable.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写分享是否启用（0禁用、1启用 同时只有一条分享为启用状态）"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@isenable", isenable));
        lpar.Add(SimonDB.CreDbPar("@id", id));

        SimonDB.ExecuteNonQuery(@"update ShareLinkQRCode set isenable=@isenable where id=@id", lpar.ToArray());
        //同时只有一条分享为启用状态
        if (isenable == "1")
        {
            SimonDB.ExecuteNonQuery(@"update ShareLinkQRCode set isenable=0 where id<>@id", lpar.ToArray());
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["id"] = id;
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 奖卷兑换商品列表(分页)
    protected void ExchangeGoodsList()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string type = SimonUtils.Qnum("type");  //商品类型 1、金币；2、话费；3实物；
        string kw = Request.Params["kw"];  //(非必选)查询关键字 模糊匹配 title、des

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (type.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@type", type));
            lwhere.Add("type=@type");
        }
        if (!string.IsNullOrWhiteSpace(kw))
        {
            lpar.Add(SimonDB.CreDbPar("@kw", kw + "%"));
            lwhere.Add("(title like @kw or des like @kw)");
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
            tempdic.Add("type", DR["type"].ToString());  //商品类型 1、金币；2、话费；3实物；
            tempdic.Add("title", DR["title"].ToString());  //商品标题
            tempdic.Add("img", DR["img"].ToString());  //商品标题图片url
            tempdic.Add("des", DR["des"].ToString());  //商品描述
            tempdic.Add("inventory", DR["inventory"].ToString());  //商品库存
            tempdic.Add("prizeprice", DR["prizeprice"].ToString());  //商品奖卷兑换价格
            tempdic.Add("exchangecoin", DR["exchangecoin"].ToString());  //兑换金币额
            tempdic.Add("exchangemobilefee", DR["exchangemobilefee"].ToString());  //兑换话费额
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

    #region 奖卷兑换商品详情
    protected void ExchangeGoodsDetails()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string id = SimonUtils.Qnum("id");  //商品ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
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
        jd["results"]["exchangecoin"] = DR["exchangecoin"].ToString();
        jd["results"]["exchangemobilefee"] = DR["exchangemobilefee"].ToString();
        jd["results"]["givecoin"] = DR["givecoin"].ToString();
        jd["results"]["sort"] = DR["sort"].ToString();
        jd["results"]["updatetime"] = DR["updatetime"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 添加,编辑奖卷兑换商品
    protected void EditExchangeGoods()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string act = SimonUtils.Q("act");  //操作动作 act=add添加,act=edit修改
        string id = SimonUtils.Qnum("id");  //商品ID，添加时无需提交该参数，编辑时必须提交该参数
        string type = SimonUtils.Qnum("type");  //商品类型 1、金币; 2、话费; 3、实物;
        string title = Request.Params["title"];  //商品标题
        string img = Request.Params["img"];  //商品标题图片url
        string des = Request.Params["des"];  //商品描述
        string inventory = SimonUtils.Qnum("inventory");  //商品库存
        string prizeprice = SimonUtils.Qnum("prizeprice");  //商品奖卷兑换价格
        string exchangecoin = SimonUtils.Qnum("exchangecoin");  //兑换金币额 商品类型为1时必填
        string exchangemobilefee = SimonUtils.Qnum("exchangemobilefee");  //兑换话费额 商品类型为2时必填
        string givecoin = SimonUtils.Qnum("givecoin");  //附赠金币额
        string sort = SimonUtils.Qnum("sort");  //排序

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (act.Equals("edit", StringComparison.OrdinalIgnoreCase) && (id.Length < 1 || !SimonUtils.IsNum(id)))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写商品ID(数字类型)"));
        }
        if (type.Length < 1 || type.Equals("0"))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写商品类型(数字类型) 1、金币；2、话费；3、实物；"));
        }
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写商品标题(1-200个字符)"));
        }
        if (string.IsNullOrWhiteSpace(img))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写商品标题图片url"));
        }
        if (string.IsNullOrWhiteSpace(des))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写商品描述"));
        }
        if (inventory.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写商品库存(数字类型)"));
        }
        if (prizeprice.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写商品奖卷兑换价格(数字类型)"));
        }
        if (type == "1" && exchangecoin.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写兑换金币额(数字类型)"));
        }
        if (type == "2" && exchangemobilefee.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写兑换话费额(数字类型)"));
        }
        if (givecoin.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写附赠金币额(数字类型)"));
        }
        if (sort.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写排序(数字类型)"));
        }

        if (exchangecoin.Length < 1) exchangecoin = "0";
        if (exchangemobilefee.Length < 1) exchangemobilefee = "0";

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@type", type));
        lpar.Add(SimonDB.CreDbPar("@title", title));
        lpar.Add(SimonDB.CreDbPar("@img", img));
        lpar.Add(SimonDB.CreDbPar("@des", des));
        lpar.Add(SimonDB.CreDbPar("@inventory", inventory));
        lpar.Add(SimonDB.CreDbPar("@prizeprice", prizeprice));
        lpar.Add(SimonDB.CreDbPar("@exchangecoin", exchangecoin));
        lpar.Add(SimonDB.CreDbPar("@exchangemobilefee", exchangemobilefee));
        lpar.Add(SimonDB.CreDbPar("@givecoin", givecoin));
        lpar.Add(SimonDB.CreDbPar("@sort", sort));
        lpar.Add(SimonDB.CreDbPar("@updatetime", DateTime.Now.ToString()));

        if (act.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            if ((int)SimonDB.ExecuteScalar(@"select count(*) from ExchangeGoods where type=@type and title=@title", lpar.ToArray()) > 0)
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "同类型商品,此商品标题已存在"));
            }

            int resultid = SimonDB.Insert(@"insert into ExchangeGoods (type,title,img,des,inventory,prizeprice,exchangecoin,exchangemobilefee,givecoin,sort,updatetime) 
                                                               values (@type,@title,@img,@des,@inventory,@prizeprice,@exchangecoin,@exchangemobilefee,@givecoin,@sort,@updatetime)", lpar.ToArray());

            JsonData jd = new JsonData();
            jd["code"] = "1";
            jd["msg"] = "success";
            jd["results"] = new JsonData();
            jd["results"]["id"] = resultid.ToString();
            SimonUtils.RespWNC(jd.ToJson());
        }

        if (act.Equals("edit", StringComparison.OrdinalIgnoreCase))
        {
            lpar.Add(SimonDB.CreDbPar("@id", id));
            if ((int)SimonDB.ExecuteScalar(@"select count(*) from ExchangeGoods where type=@type and title=@title and id<>@id", lpar.ToArray()) > 0)
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "同类型商品,此商品标题已存在"));
            }

            SimonDB.ExecuteNonQuery(@"update ExchangeGoods set type=@type,title=@title,img=@img,des=@des,inventory=@inventory,prizeprice=@prizeprice,
                                                               exchangecoin=@exchangecoin,exchangemobilefee=@exchangemobilefee,givecoin=@givecoin,
                                                               sort=@sort,updatetime=@updatetime
                                                               where id=@id", lpar.ToArray());

            JsonData jd = new JsonData();
            jd["code"] = "1";
            jd["msg"] = "success";
            jd["results"] = new JsonData();
            jd["results"]["id"] = id;
            SimonUtils.RespWNC(jd.ToJson());
        }
    }
    #endregion

    #region 删除奖卷兑换商品
    protected void DelExchangeGoods()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string id = SimonUtils.Qnum("id");  //商品ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (id.Length < 1 || !SimonUtils.IsNum(id))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "商品ID(数字类型)错误"));
        }

        SimonDB.ExecuteNonQuery(@"delete from ExchangeGoods where id=@id", new DbParameter[] { SimonDB.CreDbPar("@id", id) });

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["id"] = id;
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 奖卷兑换订单列表(分页)
    protected void ExchangeOrderList()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string userid = SimonUtils.Qnum("userid");  //(非必选)用户ID
        string goodsid = SimonUtils.Qnum("goodsid");  //(非必选)商品ID
        string goodstype = SimonUtils.Qnum("goodstype");  //(非必选)商品类型 1、金币；2、话费；3实物；
        string state = SimonUtils.Qnum("state");  //(非必选)订单状态 0 未处理，1 已处理

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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
            lwhere.Add("userid=@userid");
        }
        if (goodsid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@goodsid", goodsid));
            lwhere.Add("goodsid=@goodsid");
        }
        if (goodstype.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@goodstype", goodstype));
            lwhere.Add("goodstype=@goodstype");
        }
        if (state.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@state", state));
            lwhere.Add("state=@state");
        }

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
            tempdic.Add("goodstype", DR["goodstype"].ToString());
            tempdic.Add("goodstitle", DR["goodstitle"].ToString());
            tempdic.Add("prizeprice", DR["prizeprice"].ToString());
            tempdic.Add("exchangecoin", DR["exchangecoin"].ToString());
            tempdic.Add("exchangemobilefee", DR["exchangemobilefee"].ToString());
            tempdic.Add("givecoin", DR["givecoin"].ToString());
            tempdic.Add("realname", DR["realname"].ToString());
            tempdic.Add("mobile", DR["mobile"].ToString());
            tempdic.Add("address", DR["address"].ToString());
            tempdic.Add("orderremark", DR["orderremark"].ToString());
            tempdic.Add("orderstate", DR["orderstate"].ToString());
            tempdic.Add("osdate0", DR["osdate0"].ToString());
            tempdic.Add("osdate1", DR["osdate1"].ToString());

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

    #region 奖卷兑换订单详情
    protected void ExchangeOrderDetails()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string id = SimonUtils.Qnum("id");  //订单ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (id.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单ID错误(数字类型)"));
        }

        DataTable DT = SimonDB.DataTable(@"select * from ExchangeOrder where id=@id", new DbParameter[] { SimonDB.CreDbPar("@id", id) });
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
        jd["results"]["exchangecoin"] = DR["exchangecoin"].ToString();
        jd["results"]["exchangemobilefee"] = DR["exchangemobilefee"].ToString();
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

    #region 修改奖卷兑换订单
    protected void EditExchangeOrder()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string id = SimonUtils.Qnum("id");  //订单ID
        string orderremark = Request.Params["orderremark"];  //订单处理备注信息（商品为话费时，管理端可填写充值卡号，密码，充值网址等展现给前端）
        string orderstate = SimonUtils.Qnum("orderstate");  //订单状态 0 未处理，1 已处理

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (id.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单ID错误(数字类型)"));
        }
        if (orderstate.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单状态错误(数字类型)"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@id", id));
        lpar.Add(SimonDB.CreDbPar("@orderremark", orderremark));
        lpar.Add(SimonDB.CreDbPar("@orderstate", orderstate));

        DataTable DT = SimonDB.DataTable(@"select * from ExchangeOrder where id=@id", lpar.ToArray());
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单不存在"));
        }

        SimonDB.ExecuteNonQuery(@"update ExchangeOrder set orderremark=@orderremark,orderstate=@orderstate where id=@id", lpar.ToArray());
        if (orderstate.Equals("1"))
        {
            SimonDB.ExecuteNonQuery(@"update ExchangeOrder set osdate1=getdate() where id=@id", lpar.ToArray());
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["id"] = id;
        jd["results"]["orderstate"] = orderstate;
        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    #region 奖卷变化日志(分页)
    protected void LotteriesChangeLog()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string userid = SimonUtils.Qnum("userid");  //(非必选)游戏用户ID
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("a.CollectDate >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("a.CollectDate <= @enddt");
        }

        string _countsql = @"select count(1) from LogLotteries as a inner join TUsers as b on a.UserID=b.UserID {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by CollectDate desc) as row,
                                    b.userid,b.username,b.nickname,b.recuserid,a.PreLotteries,a.ChangeLotteries,a.CurLotteries,a.CollectDate
                                    from LogLotteries as a inner join TUsers as b on a.UserID=b.UserID {0}
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

        DataTable MoneyChangeLogDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in MoneyChangeLogDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("userid", DR["UserID"].ToString());
            tempdic.Add("username", DR["UserName"].ToString());
            tempdic.Add("nickname", DR["NickName"].ToString());
            tempdic.Add("prelotteries", DR["PreLotteries"].ToString());
            tempdic.Add("changelotteries", DR["ChangeLotteries"].ToString());
            tempdic.Add("curlotteries", DR["CurLotteries"].ToString());
            tempdic.Add("collectdate", DR["CollectDate"].ToString());

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

    #region 修改推荐人金币余额
    protected void ChangeRecUserCoinbalance()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string recuserid = SimonUtils.Qnum("recuserid");  //推荐人ID
        string changemoney = Request.Params["changemoney"];  //充值、扣除金币数(例如： 充值10000金币，扣除时为负值 -10000金)

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人ID(数字类型)"));
        }
        if (string.IsNullOrWhiteSpace(changemoney))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写充值或扣除金币数(整数类型,包含负值)"));
        }
        if (!SimonUtils.IsInt(changemoney))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值或扣除金币数为整数类型(包含负值)"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));

        DataTable RecUserDT = SimonDB.DataTable(@"select * from RecUser where recuserid=@recuserid", lpar.ToArray());
        if (RecUserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人不存在"));
        }

        DataRow RecUserDR = RecUserDT.Rows[0];
        if (long.Parse(changemoney) < 0 && -long.Parse(changemoney) > long.Parse(RecUserDR["Coinbalance"].ToString()))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "扣除金币数不能大于推荐人当前余额"));
        }

        //更新推荐人金币余额
        lpar.Add(SimonDB.CreDbPar("@changemoney", changemoney));
        SimonDB.ExecuteNonQuery(@"update RecUser set Coinbalance=Coinbalance+@changemoney where recuserid=@recuserid", lpar.ToArray());
        //写入推荐人金币余额变化日志
        lpar.Add(SimonDB.CreDbPar("@RealName", RecUserDR["RealName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@StartMoney", RecUserDR["Coinbalance"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@Remark", "管理员修改推荐人余额"));
        SimonDB.ExecuteNonQuery(@"insert into RecUserMoneyChangeLog (RecUserID,RealName,StartMoney,ChangeMoney,Remark)
                                                             values (@RecUserID,@RealName,@StartMoney,@ChangeMoney,@Remark)", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["recuserid"] = RecUserDR["RecUserID"].ToString();
        jd["results"]["startmoney"] = RecUserDR["Coinbalance"].ToString();
        jd["results"]["changemoney"] = changemoney;

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 推荐人金币余额变化日志(分页)
    protected void RecUserMoneyChangeLog()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string recuserid = SimonUtils.Qnum("recuserid");   //推荐人ID
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (recuserid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));
            lwhere.Add("recuserid=@recuserid");
        }
        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("AddDate >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("AddDate <= @enddt");
        }

        string _moneychangetotalsql = @"select coalesce(sum(ChangeMoney),0) as all_total,
                                               coalesce(sum(case when ChangeMoney > 0 then ChangeMoney else 0 end),0) as increase_total,
                                               coalesce(sum(case when ChangeMoney < 0 then ChangeMoney else 0 end),0) as reduce_total
                                        from RecUserMoneyChangeLog {0}";

        string _countsql = @"select count(1) from RecUserMoneyChangeLog {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by AddDate desc) as row,*
                                    from RecUserMoneyChangeLog {0}
                                ) where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";
        //string _listsql = @"select * from (
        //                            select row_number() over (order by AddDate desc) as row,*
        //                            from RecUserMoneyChangeLog {0}
        //                        ) as tb left join 
        //                        (select RecUserID,ShareProfitType,ShareProfitProportion from RecUser) as rectb on rectb.RecUserID=tb.RecUserID
        //                        where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _moneychangetotalsql = string.Format(_moneychangetotalsql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _moneychangetotalsql = string.Format(_moneychangetotalsql, string.Empty);
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable MoneyChangeTotalDT = SimonDB.DataTable(_moneychangetotalsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable MoneyChangeLogDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in MoneyChangeLogDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("recuserid", DR["RecUserID"].ToString());
            tempdic.Add("realname", DR["RealName"].ToString());
            //tempdic.Add("shareprofittype",DR["ShareProfitType"].ToString());
            //tempdic.Add("shareprofitproportion", DR["ShareProfitProportion"].ToString());
            tempdic.Add("startmoney", DR["StartMoney"].ToString());
            tempdic.Add("changemoney", DR["ChangeMoney"].ToString());
            tempdic.Add("adddate", DR["AddDate"].ToString());
            tempdic.Add("remark", DR["Remark"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("moneychangetotal", MoneyChangeTotalDT.Rows[0]["all_total"].ToString());
        jsondic.Add("increase_total", MoneyChangeTotalDT.Rows[0]["increase_total"].ToString());
        jsondic.Add("reduce_total", MoneyChangeTotalDT.Rows[0]["reduce_total"].ToString());
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 分润类型及比例修改
    protected void ChangeRecUserShare()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string recuserid = SimonUtils.Qnum("recuserid");  //推荐人ID
        string shareprofittype = SimonUtils.Qnum("ShareProfitType");//推荐人分润类型
        string shareprofitproportion = Request.Params["ShareProfitProportion"];//推荐人分润比例

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (shareprofittype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请选择推荐人分润类型"));
        }
        if (shareprofittype != "1" && shareprofittype != "2")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人分润不包含该类型"));
        }
        if (shareprofitproportion.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人分润比例"));
        }
        if (!SimonUtils.IsDecimal2(shareprofitproportion))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分润比例应为小数类型(小数点后最多可保留3位)"));
        }
        if (Decimal.Parse(shareprofitproportion) < 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "分润比例须大于0"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));

        DataTable RecUserDT = SimonDB.DataTable(@"select * from RecUser where recuserid=@recuserid", lpar.ToArray());
        if (RecUserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人不存在"));
        }

        //更新推荐人分润类型和比例
        lpar.Add(SimonDB.CreDbPar("@ShareProfitType", shareprofittype));
        lpar.Add(SimonDB.CreDbPar("@shareprofitproportion", shareprofitproportion));
        SimonDB.ExecuteNonQuery(@"update RecUser set shareprofittype=@shareprofittype and shareprofitproportion=@shareprofitproportion where recuserid=@recuserid", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "修改推荐人分润成功";
        jd["results"] = "null";
        SimonUtils.RespWNC(jd.ToJson());
    }

    #endregion

    #region 游戏房间列表
    protected void GameRoomList()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }

        DataTable ListDT = SimonDB.DataTable(@"select * from TGameRoomInfo order by GameNameID desc, RoomID desc");

        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("roomid", DR["RoomID"].ToString());
            tempdic.Add("roomname", DR["RoomName"].ToString());
            tempdic.Add("gamenameid", DR["GameNameID"].ToString());
            tempdic.Add("onlinecount", DR["OnlineCount"].ToString());
            tempdic.Add("updatetime", DR["UpdateTime"].ToString());

            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "success");
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 游戏房间设置详情
    protected void GameRoomConfigDetails()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string roomid = SimonUtils.Qnum("roomid");  //订单ID

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (roomid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间ID错误(数字类型)"));
        }

        DataTable DT = SimonDB.DataTable(@"select * from TGameRoomInfo where roomid=@roomid", new DbParameter[] { SimonDB.CreDbPar("@roomid", roomid) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间不存在"));
        }

        DataRow DR = DT.Rows[0];

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["roomid"] = DR["roomid"].ToString();
        jd["results"]["cfg_roomtype"] = DR["cfg_RoomType"].ToString();
        jd["results"]["cfg_m_icellscore"] = DR["cfg_m_iCellscore"].ToString();
        jd["results"]["cfg_m_imaxcellfire"] = DR["cfg_m_iMaxCellFire"].ToString();
        jd["results"]["cfg_m_iminxcellfire"] = DR["cfg_m_iMinxCellFire"].ToString();
        jd["results"]["cfg_m_igoldcoin"] = DR["cfg_m_iGoldCoin"].ToString();
        jd["results"]["cfg_m_ifishcoin"] = DR["cfg_m_iFishCoin"].ToString();
        jd["results"]["cfg_tax"] = DR["cfg_Tax"].ToString();
        jd["results"]["cfg_goldtoniuniu"] = DR["cfg_GoldToNIuniu"].ToString();
        jd["results"]["cfg_upscore"] = DR["cfg_UpScore"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 编辑游戏房间设置
    protected void EditGameRoomConfig()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string roomid = SimonUtils.Qnum("roomid");  //房间ID
        string cfg_roomtype = Request.Params["cfg_roomtype"];  //游戏难度（1,2,3）
        string cfg_m_icellscore = SimonUtils.Qnum("cfg_m_icellscore");  //单元炮火大小（就是点一下加炮加多少）
        string cfg_m_imaxcellfire = SimonUtils.Qnum("cfg_m_imaxcellfire");  //最大炮火大小
        string cfg_m_iminxcellfire = SimonUtils.Qnum("cfg_m_iminxcellfire");  //最小炮火大小
        string cfg_m_igoldcoin = SimonUtils.Qnum("cfg_m_igoldcoin");  //金币比例
        string cfg_m_ifishcoin = SimonUtils.Qnum("cfg_m_ifishcoin");  //鱼币比例
        string cfg_tax = SimonUtils.Qnum("cfg_tax");  //房间税，每一局收多少游戏币
        string cfg_goldtoniuniu = SimonUtils.Qnum("cfg_goldtoniuniu");  //1金币等于多少牛牛币
        string cfg_upscore = SimonUtils.Qnum("cfg_upscore");  //牛牛一次上分上多少金币

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (roomid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单ID错误(数字类型)"));
        }
        if (cfg_roomtype.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写游戏难度(数字类型)(1,2,3)"));
        }
        if (cfg_m_icellscore.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写单元炮火大小(数字类型)(点一下加炮加多少)"));
        }
        if (cfg_m_imaxcellfire.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写最大炮火大小(数字类型)"));
        }
        if (cfg_m_iminxcellfire.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写最小炮火大小(数字类型)"));
        }
        if (cfg_m_igoldcoin.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写金币比例(数字类型)"));
        }
        if (cfg_m_ifishcoin.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写鱼币比例(数字类型)"));
        }
        if (cfg_tax.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写房间税(数字类型)(每一局收多少游戏币)"));
        }
        if (cfg_goldtoniuniu.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写1金币等于多少牛牛币(数字类型)"));
        }
        if (cfg_upscore.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写牛牛一次上分上多少金币(数字类型)"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@roomid", roomid));
        lpar.Add(SimonDB.CreDbPar("@cfg_roomtype", cfg_roomtype));
        lpar.Add(SimonDB.CreDbPar("@cfg_m_icellscore", cfg_m_icellscore));
        lpar.Add(SimonDB.CreDbPar("@cfg_m_imaxcellfire", cfg_m_imaxcellfire));
        lpar.Add(SimonDB.CreDbPar("@cfg_m_iminxcellfire", cfg_m_iminxcellfire));
        lpar.Add(SimonDB.CreDbPar("@cfg_m_igoldcoin", cfg_m_igoldcoin));
        lpar.Add(SimonDB.CreDbPar("@cfg_m_ifishcoin", cfg_m_ifishcoin));
        lpar.Add(SimonDB.CreDbPar("@cfg_tax", cfg_tax));
        lpar.Add(SimonDB.CreDbPar("@cfg_goldtoniuniu", cfg_goldtoniuniu));
        lpar.Add(SimonDB.CreDbPar("@cfg_upscore", cfg_upscore));

        DataTable DT = SimonDB.DataTable(@"select * from TGameRoomInfo where roomid=@roomid", new DbParameter[] { SimonDB.CreDbPar("@roomid", roomid) });
        if (DT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "房间不存在"));
        }

        SimonDB.ExecuteNonQuery(@"update TGameRoomInfo set cfg_roomtype=@cfg_roomtype,cfg_m_icellscore=@cfg_m_icellscore,
                                  cfg_m_imaxcellfire=@cfg_m_imaxcellfire,cfg_m_iminxcellfire=@cfg_m_iminxcellfire,
                                  cfg_m_igoldcoin=@cfg_m_igoldcoin,cfg_m_ifishcoin=@cfg_m_ifishcoin,
                                  cfg_tax=@cfg_tax,cfg_goldtoniuniu=@cfg_goldtoniuniu,cfg_upscore=@cfg_upscore
                                  where roomid=@roomid", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["roomid"] = roomid;
        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion



    //以下为推广员提款信息
    #region 获取取款订单申请列表（未处理，含分页）
    protected void GetCashOrderDataList()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        string _cashordersql = @"select * from RecUserCashOrder where Status=1";
        string _countsql = @"select count(1) from RecUserCashOrder  where Status=1";
        string _listsql = @"select * from (
                                    select row_number() over (order by AddTime desc) as row,*
                                    from RecUserCashOrder  where Status=1
                                )as a where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        DataTable CashOrderDT = SimonDB.DataTable(_cashordersql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable CashOrderLogDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in CashOrderLogDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("OrderNumID", DR["OrderNumID"].ToString());
            tempdic.Add("RecUserID", DR["RecUserID"].ToString());
            tempdic.Add("OrderGold", DR["OrderGold"].ToString());
            tempdic.Add("AddTime", DR["AddTime"].ToString());
            tempdic.Add("Status", DR["Status"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }

    #endregion

    #region 订单处理（status分为：2、同意，3、拒绝并退款，4、拒绝不退款。3和4均需要备注原因）
    protected void CashOrderDeal()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string ordernumid = Request.Params["ordernumid"];
        string status = SimonUtils.Qnum("status");  //处理订单的类型（状态：1：申请中，2：成功，3：失败[退还金币]，4：失败[不退还金币]）
        string remark = Request.Params["remark"];

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (ordernumid.Length<1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请选择要处理的订单编号"));
        }
        if (status.Length<1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请选择要处理类型"));
        }
        if (status!="2"&&status!="3"&&status!="4")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "处理选项有误"));
        }
        if ((status=="3"||status=="4")&& remark.Length < 1)
        {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请告知拒绝原因"));
        }
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@ordernumid", ordernumid));
        //订单表查询
        DataTable CashOrderDT = SimonDB.DataTable(@"select * from RecUserCashOrder where ordernumid=@ordernumid", lpar.ToArray());
        if (CashOrderDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "订单号不存在"));
        }
        DataRow DR = CashOrderDT.Rows[0];
        lpar.Add(SimonDB.CreDbPar("@recuserid", DR["RecUserID"]));
        //推广员金币表查询
        DataTable RecUserDT = SimonDB.DataTable(@"select * from RecUserGold where RecUserID=@recuserid", lpar.ToArray());
        DataRow RecDR = RecUserDT.Rows[0];
        if (int.Parse(RecDR["Status"].ToString()) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "该推广员账户状态异常（冻结状态）"));
        }
        lpar.Add(SimonDB.CreDbPar("@status", status));
        lpar.Add(SimonDB.CreDbPar("@remark", remark));
        
        string logstatus = "";
        switch (status)
        {
            case "2":
                logstatus = "2";
                break;
            case "3":
                logstatus = "3";
                break;
            case "4":
                logstatus = "4";
                break;
        }
        //更新推广员金币订单表
        SimonDB.ExecuteNonQuery(@"update RecUserCashOrder set status=@status,remark=@remark where ordernumid=@ordernumid", lpar.ToArray());
        //写入推广员金币变化日志
        lpar.Add(SimonDB.CreDbPar("@logstatus", logstatus));
        SimonDB.ExecuteNonQuery(@"update RecUserCashLog set status=@logstatus,updatetime=getdate() where ordernumid=@ordernumid", lpar.ToArray());
        //失败不扣除，需要返还到推广员账户
        lpar.Add(SimonDB.CreDbPar("@ordergold", DR["OrderGold"].ToString()));
        if (status == "3")
        {
            //更新推广员金币表
            SimonDB.ExecuteNonQuery(@"update RecUserGold set CanCash=CanCash+@ordergold,HadCash=HadCash-@ordergold where recuserid=@recuserid", lpar.ToArray());
        }

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "订单处理完毕";
        jd["results"] = "null";

        SimonUtils.RespWNC(jd.ToJson());

    }
    #endregion

    //以下为用户相关信息

    #region 用户游戏记录
    protected void GetUserGameData()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string userid = SimonUtils.Qnum("uid");  //玩家ID（可为空，为空则查询所有玩家）
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间(默认当天)
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间(默认当天)
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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
            lwhere.Add("userid=@userid");
        }

        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("EndTime >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("EndTime <= @enddt");
        }
        else
        {
            lwhere.Add("datediff(DD,EndTime,getdate())=0 ");
        }

        string _GameDataTotalsql = @"SELECT  * FROM  Web_vGameRecord  {0}";

        string _countsql = @"SELECT  count(1) FROM  Web_vGameRecord  {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by EndTime desc) as row,*
                                    from Web_vGameRecord  {0} ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _GameDataTotalsql = string.Format(_GameDataTotalsql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _GameDataTotalsql = string.Format(_GameDataTotalsql, string.Empty);
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable GameDataTotalDT = SimonDB.DataTable(_GameDataTotalsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable GameDataListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in GameDataListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("RoomName", DR["RoomName"].ToString());
            tempdic.Add("DeskIndex", DR["DeskIndex"].ToString());
            tempdic.Add("GameTime", DR["GameTime"].ToString());
            tempdic.Add("SrcMoney", DR["SrcMoney"].ToString());
            tempdic.Add("ChangeMoney", DR["ChangeMoney"].ToString());
            tempdic.Add("ChangeTax", DR["ChangeTax"].ToString());
            tempdic.Add("EndTime", DR["EndTime"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));

    }

    #endregion

    #region 用户登陆记录
    protected void GetUserLoginData()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string userid = SimonUtils.Qnum("uid");  //玩家ID（可为空，为空则查询所有玩家）
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间(默认当天)
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间(默认当天)
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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
            lwhere.Add("userid=@userid");
        }

        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("LastLoginTM >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("LastLoginTM <= @enddt");
        }
        else
        {
            lwhere.Add("datediff(DD,LastLoginTM,getdate())=0 ");
        }

        string _UserLoginsql = @"SELECT  * FROM  Web_VLoginRecord  {0}";

        string _countsql = @"SELECT  count(1) FROM  Web_VLoginRecord  {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by LastLoginTM desc) as row,*
                                    from Web_VLoginRecord  {0} ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _UserLoginsql = string.Format(_UserLoginsql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _UserLoginsql = string.Format(_UserLoginsql, string.Empty);
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable UserLoginTotalDT = SimonDB.DataTable(_UserLoginsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable UserLoginListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in UserLoginListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("RegisterTM", DR["RegisterTM"].ToString());
            tempdic.Add("Disabled", DR["Disabled"].ToString());
            tempdic.Add("LastLoginIP", DR["LastLoginIP"].ToString());
            tempdic.Add("LastLoginTM", DR["LastLoginTM"].ToString());
            tempdic.Add("IsLimitIP", DR["IsLimitIP"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 当前在线玩家
    protected void GetOnlinePlayersData()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        string _OnlinUserql = @"SELECT  * FROM  Web_VGetUserOnline";

        string _countsql = @"SELECT  count(1) FROM  Web_VGetUserOnline";
        string _listsql = @"select * from (
                                    select row_number() over (order by EndTime desc) as row,*
                                    from Web_VGetUserOnline) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        DataTable OnlineUserTotalDT = SimonDB.DataTable(_OnlinUserql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable OnlineUserListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in OnlineUserListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("GameCount", DR["GameCount"].ToString());
            tempdic.Add("WalletMoney", DR["WalletMoney"].ToString());
            tempdic.Add("SumMoney", DR["SumMoney"].ToString());
            tempdic.Add("ComName", DR["ComName"].ToString());
            tempdic.Add("RoomName", DR["RoomName"].ToString());
            tempdic.Add("LoginIP", DR["LoginIP"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 用户转账记录
    protected void GetUserTransferLog()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string userid = SimonUtils.Qnum("uid");//玩家ID（可为空，为空则查询所有玩家）
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间(默认当天)
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间(默认当天)
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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
            lwhere.Add("userid=@userid");
        }

        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("TransTime >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("TransTime <= @enddt");
        }
        else
        {
            lwhere.Add("datediff(DD,TransTime,getdate())=0 ");
        }

        string _GameDataTotalsql = @"SELECT  * FROM  Web_VTransLog  {0}";

        string _countsql = @"SELECT  count(1) FROM  Web_VTransLog  {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by TransTime desc) as row,*
                                    from Web_VTransLog  {0} ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _GameDataTotalsql = string.Format(_GameDataTotalsql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _GameDataTotalsql = string.Format(_GameDataTotalsql, string.Empty);
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable GameDataTotalDT = SimonDB.DataTable(_GameDataTotalsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable GameDataListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in GameDataListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("TransBefore", DR["TransBefore"].ToString());
            tempdic.Add("TransAfter", DR["TransAfter"].ToString()); 
            tempdic.Add("UserZZID", DR["UserZZID"].ToString());
            tempdic.Add("UserNameZZ", DR["UserNameZZ"].ToString());
            tempdic.Add("NickNameZZ", DR["NickNameZZ"].ToString());
            tempdic.Add("ZZ_TransBefore", DR["ZZ_TransBefore"].ToString());
            tempdic.Add("ZZ_TransAfter", DR["ZZ_TransAfter"].ToString());
            tempdic.Add("Money", DR["Money"].ToString());
            tempdic.Add("Tax", DR["Tax"].ToString());
            tempdic.Add("Success", DR["Success"].ToString());
            tempdic.Add("TransTime", DR["TransTime"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 用戶存取记录
    protected void GetUserBankWalletLog()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string userid = SimonUtils.Qnum("uid");//玩家ID（可为空，为空则查询所有玩家）
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间(默认当天)
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间(默认当天)
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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
            lwhere.Add("userid=@userid");
        }

        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("TimeEx >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("TimeEx <= @enddt");
        }
        else
        {
            lwhere.Add("datediff(DD,TimeEx,getdate())=0 ");
        }

        string _GameDataTotalsql = @"SELECT  * FROM  Web_vBankMoneyOpera  {0}";

        string _countsql = @"SELECT  count(1) FROM  Web_vBankMoneyOpera  {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by TimeEx desc) as row,*
                                    from Web_vBankMoneyOpera  {0} ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _GameDataTotalsql = string.Format(_GameDataTotalsql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _GameDataTotalsql = string.Format(_GameDataTotalsql, string.Empty);
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable GameDataTotalDT = SimonDB.DataTable(_GameDataTotalsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable GameDataListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in GameDataListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("MoneyInBank", DR["MoneyInBank"].ToString());   //保管箱金币
            tempdic.Add("MoneyInRoom", DR["MoneyInRoom"].ToString());   //钱包金币
            tempdic.Add("OutMoney", DR["OutMoney"].ToString());  //存入到保管箱
            tempdic.Add("InMoney", DR["InMoney"].ToString());   //取出到钱包
            tempdic.Add("TimeEx", DR["TimeEx"].ToString());   //操作时间
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    //以下为游戏相关信息

    #region 获取游戏列表
    protected void GetGameRoomList()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        string _Gamesql = @"SELECT  * FROM  VRoomList";

        string _countsql = @"SELECT  count(1) FROM  VRoomList";
        string _listsql = @"select * from (
                                    select row_number() over (order by EndTime desc) as row,*
                                    from VRoomList ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        DataTable GameTotalDT = SimonDB.DataTable(_Gamesql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable GameListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in GameListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("NameID", DR["NameID"].ToString());
            tempdic.Add("GameName", DR["GameName"].ToString());
            tempdic.Add("RoomID", DR["RoomID"].ToString());
            tempdic.Add("RoomName", DR["RoomName"].ToString());
            tempdic.Add("DeskPeople", DR["DeskPeople"].ToString());
            tempdic.Add("ServiceName", DR["ServiceName"].ToString());
            tempdic.Add("ServerIP", DR["ServerIP"].ToString());
            tempdic.Add("EnableRoom", DR["EnableRoom"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));

    }
    #endregion

    #region 桌子当前输赢信息
    protected void GetRoomTableDataNow()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        string _TableWinLosesql = @"select a.RoomID,a.RoomName,a.OnlineCount,b.Desk,isnull(b.WinTotal,0) as WinTotal,isnull(b.LoseTotal,0) as LoseTotal,b.TaskScore,b.CaijinScore from( select * from TGameRoomInfo  ) as a left join (select * from LogTRoomWinlose ) as b on b.RoomId=a.RoomID";
        string _countsql = @"SELECT  count(1) FROM  (" + _TableWinLosesql + ") as newtb";
        string _listsql = @"select * from (
                                    select row_number() over (order by RoomID,Desk) as row,*
                                    from (" + _TableWinLosesql + ") as newtb ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";
       
        DataTable TabelListTotalDT = SimonDB.DataTable(_TableWinLosesql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable TableWinLoseListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in TableWinLoseListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RoomID", DR["RoomID"].ToString());
            tempdic.Add("RoomName", DR["RoomName"].ToString());
            tempdic.Add("OnlineCount", DR["OnlineCount"].ToString());
            tempdic.Add("Table", DR["Desk"].ToString());
            tempdic.Add("WinTotal", DR["WinTotal"].ToString());   //系统赢分
            tempdic.Add("LoseTotal", DR["LoseTotal"].ToString());  //系统输分
            tempdic.Add("TaskScore", DR["TaskScore"].ToString());  //税费
            tempdic.Add("CaijinScore",DR["CaijinScore"].ToString());  //系统彩金
            Int64 lose = Convert.ToInt64(DR["LoseTotal"]);
            if (lose==0)
            {
                tempdic.Add("JiLv", "0"); 
            }
            else
            {
                decimal jilv = Math.Round((decimal)Convert.ToInt64(DR["LoseTotal"])/ Convert.ToInt64(DR["WinTotal"]),4);
                string JiLv =( jilv * 100).ToString() + "%";
                tempdic.Add("JiLv", JiLv);  //系统几率
            }
            tempdic.Add("DifTotal",(Convert.ToInt64(DR["WinTotal"])- Convert.ToInt64(DR["LoseTotal"])+ Convert.ToInt64(DR["TaskScore"])- Convert.ToInt64(DR["CaijinScore"])).ToString());   //系统净值
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));

    }
    #endregion

    #region 桌子详细输赢信息（按天查询，只能单日查询，一天有48条记录）
    protected void GetRoomTableDataDetail()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string roomid = SimonUtils.Qnum("roomid");  //房间ID
        string tableid = SimonUtils.Qnum("tableid");  //桌子ID
        string selectdt = Request.Params["selectdt"];  //(非必选)日期筛选
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (roomid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@roomid", roomid));
            lwhere.Add("b.RoomID=@roomid");
        }
        else
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏房间未选择"));
        }
        if (tableid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@tableid", tableid));
            lwhere.Add("b.Desk=@tableid");
        }
        else
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "桌子未选择"));
        }
        if (!string.IsNullOrWhiteSpace(selectdt) && SimonUtils.IsStringDate(selectdt))
        {
            lpar.Add(SimonDB.CreDbPar("@selectdt", selectdt));
            lwhere.Add("b.CollectDate >= @selectdt");
        }
        else
        {
            lwhere.Add("datediff(DD,b.CollectDate,getdate())=0 ");
        }

        string _TableWinLoseDetailsql = @"select a.RoomID,a.RoomName,a.OnlineCount,b.Desk,isnull(b.WinTotal,0) as WinTotal,isnull(b.LoseTotal,0) as LoseTotal,b.TaskScore,b.CaijinScore from( select * from TGameRoomInfo  ) as a left join (select * from LogTRoomTotalWinloseNew ) as b on b.RoomId=a.RoomID {0}";
        string _countsql = @"SELECT  count(1) FROM  (" + _TableWinLoseDetailsql + ") as newtb";
        string _listsql = @"select * from (
                                    select row_number() over (order by RoomID,Desk) as row,*
                                    from (" + _TableWinLoseDetailsql + ") as newtb ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";
        
        string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
        _TableWinLoseDetailsql = string.Format(_TableWinLoseDetailsql, _sqlwhere);
        _countsql = string.Format(_countsql, _sqlwhere);
        _listsql = string.Format(_listsql, _sqlwhere);

        DataTable TableWinLoseDetailDT = SimonDB.DataTable(_TableWinLoseDetailsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable TableWinLoseDetailListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in TableWinLoseDetailListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("CollectDate", DR["CollectDate"].ToString());
            tempdic.Add("RoomID", DR["RoomID"].ToString());
            tempdic.Add("RoomName", DR["RoomName"].ToString());
            tempdic.Add("OnlineCount", DR["OnlineCount"].ToString());
            tempdic.Add("Table", DR["Desk"].ToString());
            tempdic.Add("WinTotal", DR["WinTotal"].ToString());   //系统赢分
            tempdic.Add("LoseTotal", DR["LoseTotal"].ToString());  //系统输分
            tempdic.Add("TaskScore", DR["TaskScore"].ToString());  //税费
            tempdic.Add("CaijinScore", DR["CaijinScore"].ToString());  //系统彩金
            Int64 lose = Convert.ToInt64(DR["LoseTotal"]);
            if (lose == 0)
            {
                tempdic.Add("JiLv", "0");
            }
            else
            {
                decimal jilv = Math.Round((decimal)Convert.ToInt64(DR["LoseTotal"]) / Convert.ToInt64(DR["WinTotal"]), 4);
                string JiLv = (jilv * 100).ToString() + "%";
                tempdic.Add("JiLv", JiLv);  //系统几率
            }
            tempdic.Add("DifTotal", (Convert.ToInt64(DR["WinTotal"]) - Convert.ToInt64(DR["LoseTotal"]) + Convert.ToInt64(DR["TaskScore"]) - Convert.ToInt64(DR["CaijinScore"])).ToString());   //系统净值
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));

    }
    #endregion

    //以下为数据分析

    #region 每日数据统计
    protected void GetDayAllData()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间(默认当天)
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间(默认当天)
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("ReportDate >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("ReportDate <= @enddt");
        }
        else
        {
            lwhere.Add("datediff(DD,ReportDate,getdate())=1 ");
        }

        string _DayDatasql = @"SELECT  * FROM  Web_NewAdmin_DayReport  {0}";

        string _countsql = @"SELECT  count(1) FROM  Web_NewAdmin_DayReport  {0}";
        string _listsql = @"select * from ( select row_number() over (order by ReportDate desc) as row,* from Web_vGameRecord  {0} ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _DayDatasql = string.Format(_DayDatasql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _DayDatasql = string.Format(_DayDatasql, string.Empty);
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable DayDataDT = SimonDB.DataTable(_DayDatasql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable DayDataListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DayDataListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("ReportDate", Convert.ToDateTime(DR["ReportDate"]).ToShortDateString());
            tempdic.Add("AvgOnline", DR["AvgOnline"].ToString());
            tempdic.Add("MaxOnline", DR["MaxOnline"].ToString());
            tempdic.Add("MinOnline", DR["MinOnline"].ToString());
            tempdic.Add("LoginNum", DR["LoginNum"].ToString());
            tempdic.Add("ActiveNum", DR["ActiveNum"].ToString());
            tempdic.Add("ActiveGameCount", DR["ActiveGameCount"].ToString());
            tempdic.Add("NewUserNum", DR["NewUserNum"].ToString());
            tempdic.Add("PayNum", DR["PayNum"].ToString());
            tempdic.Add("PayMoneyCount", DR["PayMoneyCount"].ToString());
            tempdic.Add("PayCount", DR["PayCount"].ToString());
            tempdic.Add("GameCount", DR["GameCount"].ToString());
            tempdic.Add("TaxCount", DR["TaxCount"].ToString());
            tempdic.Add("RegNum", DR["RegNum"].ToString());
            tempdic.Add("RecUserID", DR["RecUserID"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 用户注册分析
    protected void GetUserRegAnaly()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间(默认当天)
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间(默认当天)
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
            lwhere.Add("RegisterTM >= @startdt");
        }
        else
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", "1970-01-01"));
            lwhere.Add("RegisterTM >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("RegisterTM <= @enddt");
        }
        else
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", DateTime.Now.ToShortDateString()));
            lwhere.Add("RegisterTM <= @enddt");
        }

        string _DayDatasql = @"SELECT  * FROM  NewAdminUserReg  {0}";

        string _countsql = @"SELECT  count(1) FROM  NewAdminUserReg  {0}";
        string _listsql = @"select * from ( select row_number() over (order by RegisterTM desc) as row,* from NewAdminUserReg  {0} ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _DayDatasql = string.Format(_DayDatasql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _DayDatasql = string.Format(_DayDatasql, string.Empty);
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable DayDataDT = SimonDB.DataTable(_DayDatasql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable DayDataListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in DayDataListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("RealName", DR["RealName"].ToString());
            tempdic.Add("BankMoney", DR["BankMoney"].ToString());
            tempdic.Add("WalletMoney", DR["WalletMoney"].ToString());
            tempdic.Add("SumMoney", DR["SumMoney"].ToString());
            tempdic.Add("RegisterTM", DR["RegisterTM"].ToString());
            tempdic.Add("RegisterIP", DR["RegisterIP"].ToString());
            tempdic.Add("GameCount", DR["GameCount"].ToString());
            tempdic.Add("GameTime", DR["GameTime"].ToString());
            tempdic.Add("OnlineStatus", DR["OnlineStatus"].ToString());
            tempdic.Add("LoginCount", DR["LoginCount"].ToString());
            tempdic.Add("RecUserID", DR["RecUserID"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 用户登陆分析
    protected void GetUserLoginAnaly()
    {
        CheckAdminSign();
        string token = Request.Params["token"];  //登录验证token
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间(默认当天)
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间(默认当天)
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string adminuserid = "0";
        if (!CheckAdminLogin(token, out adminuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
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

        if (!string.IsNullOrWhiteSpace(startdt) && SimonUtils.IsStringDate(startdt))
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", startdt));
        }
        else
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", "1970-01-01"));
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
        }
        else
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", DateTime.Now.ToShortDateString()));
        }

        string _UserLoginsql = @"select K.UserID,K.UserName,e.NickName,c.BankMoney,c.WalletMoney,
                          (SELECT     CASE WHEN
                                                       (SELECT     BankVersion
                                                         FROM          dbo.Web_Config) = 1 THEN c.WalletMoney + c.BankMoney WHEN
                                                       (SELECT     BankVersion
                                                         FROM          dbo.Web_Config) = 2 THEN
                                                       (SELECT     ISNULL(SUM(walletmoney), 0) AS walletmoeny
                                                         FROM          dbo.TBankWallet
                                                         WHERE      UserID = c.UserID) + c.BankMoney ELSE 0 END AS SumMoney) AS SumMoney,K.FirstLoginTime,K.LastLoginTime,K.LoginNum,isnull(G.OnlineTime,0) as OnlineTime,isnull(G.GameCount,0) as GameCount,isnull(R.TotalPayMoney,0) as TotalPayMoney,(case when isnull(TWLoginRecord.UserID,0)>0 then '在线' else '不在线' end ) as OnlineStatus,e.RecUserID from (select a.UserID,Min(LastLoginTM) as FirstLoginTime,Max(LastLoginTM) as LastLoginTime,count(*) as LoginNum,b.UserName from TLoginRecord a inner join TUsers b on a.UserID=b.UserID where b.IsRobot=0 and a.LastLoginTM>=@startdt and a.LastLoginTM<=@enddt group by a.UserID,b.UserName) as K
		inner join TUserInfo c on K.UserID=c.UserID
		inner join Web_Users d on K.UserID=d.UserID
        inner join TUsers e on K.UserID=e.UserID
		left join (select count(*) as GameCount,isnull(sum(GameTime),0)/60 as OnlineTime,UserID from TChangeRecord t1 inner join TChangeRecordUser t2 on t1.ID=t2.RecordIndex  group by UserID) as G on G.UserID=K.UserID
		left join (select isnull(sum(PayMoney),0) as TotalPayMoney,UserID from Web_VAnaly_PayList where  AddTime>=@startdt and AddTime<=@enddt group by UserID) as R on R.UserID=K.UserID
		left join TWLoginRecord on TWLoginRecord.UserID=K.UserID ";

        string _countsql = @"SELECT  count(1) FROM  (" + _UserLoginsql + ") as newtb  {0}";
        string _listsql = @"select * from ( select row_number() over (order by LastLoginTime desc) as row,* from  (" + _UserLoginsql + ") as newtb  {0} ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        DataTable UserLoginDT = SimonDB.DataTable(_UserLoginsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable UserLoginListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in UserLoginListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("BankMoney", DR["BankMoney"].ToString());
            tempdic.Add("WalletMoney", DR["WalletMoney"].ToString());
            tempdic.Add("SumMoney", DR["SumMoney"].ToString());
            tempdic.Add("FirstLoginTime", DR["FirstLoginTime"].ToString());
            tempdic.Add("LastLoginTime", DR["LastLoginTime"].ToString());
            tempdic.Add("LoginNum", DR["LoginNum"].ToString());
            tempdic.Add("OnlineTime", DR["OnlineTime"].ToString());
            tempdic.Add("GameCount", DR["GameCount"].ToString());
            tempdic.Add("TotalPayMoney", DR["TotalPayMoney"].ToString());
            tempdic.Add("OnlineStatus", DR["OnlineStatus"].ToString());
            tempdic.Add("RecUserID", DR["RecUserID"].ToString());
            resultslist.Add(tempdic);
        }

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion



    //protected void SendCmd()
    //{
    //    string result = SocketHelper.Send("101.37.38.153", 3015, "test-测试");
    //    SimonUtils.RespWNC(result);
    //}
}