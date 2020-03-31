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

public partial class Api_RecUser : System.Web.UI.Page
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

    #region 推荐人登录检查(辅助方法)
    /// <summary>
    /// 推荐人登录检查
    /// </summary>
    /// <returns></returns>
    private bool CheckRecUserLogin(string token, out string outrecuserid)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            outrecuserid = "0";
            return false;
        }
        string _detoken = string.Empty;
        try
        {
            _detoken = SimonDES.Decrypt(token);
        }
        catch (Exception)
        {
            outrecuserid = "0";
            return false;
        }
        string[] _detokenarr = _detoken.Split('|');
        if (_detokenarr.Length != 2)
        {
            outrecuserid = "0";
            return false;
        }
        string _recuserid = _detokenarr[0];
        string _logindt = _detokenarr[1];
        int _cookieexp = CurrSite.CookieExp;
        if (_cookieexp == 0) _cookieexp = 720; //默认时效时间720分钟(12小时)
        if (SimonUtils.IsInt(_recuserid) && SimonUtils.IsStringDate(_logindt) && DateTime.Parse(_logindt).AddMinutes(_cookieexp) >= DateTime.Now)
        {
            DbParameter[] parms = { SimonDB.CreDbPar("@recuserid", _recuserid), SimonDB.CreDbPar("@token", token) };
            if (((int)SimonDB.ExecuteScalar(@"select count(*) from RecUser where recuserid=@recuserid and token=@token", parms)) > 0)
            {
                outrecuserid = _recuserid;
                return true;
            }
        }
        outrecuserid = "0";
        return false;
    }
    #endregion

    #region 推荐人管理端API验签检查(辅助方法)
    /// <summary>
    /// 推荐人管理端API验签检查(辅助方法)
    /// </summary>
    private void CheckRecUserSign()
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
        if (!CurrSite.RecUserVerifySign(sign, t))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "签名错误"));
        }
    }
    #endregion


    #region 推荐人登陆
    protected void RecUserLogin()
    {
        CheckRecUserSign();
        string rid = Request.Params["rid"];  //推荐人ID
        string rpwd = Request.Params["rpwd"];  //密码(明文)

        if (string.IsNullOrWhiteSpace(rid) || !SimonUtils.IsInt(rid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写推荐人ID(数字类型)"));
        }
        if (string.IsNullOrWhiteSpace(rpwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写密码"));
        }

        DbParameter[] rparms = { SimonDB.CreDbPar("@recuserid", rid), SimonDB.CreDbPar("@recuserpwd", SimonUtils.EnCodeMD5(rpwd)) };
        DataTable RDT = SimonDB.DataTable(@"select * from RecUser where recuserid=@recuserid and recuserpwd=@recuserpwd", rparms);
        if (RDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID或密码错误"));
        }

        DataRow RDR = RDT.Rows[0];
        //生成登陆token并加密,格式为 推荐人ID|登录时间
        string token = SimonDES.Encrypt(string.Format("{0}|{1}", RDR["recuserid"].ToString(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

        //更新token
        DbParameter[] updateparms = { 
                SimonDB.CreDbPar("@token", token),
                SimonDB.CreDbPar("@recuserid", RDR["RecUserID"].ToString())};
        SimonDB.ExecuteNonQuery(@"update RecUser set token=@token where recuserid=@recuserid", updateparms);

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["token"] = token;
        jd["results"]["rid"] = RDR["RecUserID"].ToString();
        jd["results"]["realname"] = RDR["RealName"].ToString();
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 推荐人退出
    protected void RecUserLogout()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }

        //设置token失效
        DbParameter[] updateparms = { SimonDB.CreDbPar("@token", token) };
        SimonDB.ExecuteNonQuery(@"update RecUser set token=null where token=@token", updateparms);

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = "null";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 推荐人修改密码
    protected void RecUserEdit()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string rpwd = Request.Params["rpwd"];  //原密码(明文)
        string newrpwd = Request.Params["newrpwd"];  //新密码(明文)

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (string.IsNullOrWhiteSpace(rpwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写原密码"));
        }
        if (string.IsNullOrWhiteSpace(newrpwd))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写新密码"));
        }

        DbParameter[] rparms = { SimonDB.CreDbPar("@recuserid", recuserid), SimonDB.CreDbPar("@recuserpwd", SimonUtils.EnCodeMD5(rpwd)) };
        DataTable RDT = SimonDB.DataTable(@"select * from RecUser where recuserid=@recuserid and recuserpwd=@recuserpwd", rparms);
        if (RDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID或原密码错误"));
        }

        DbParameter[] updateparms = { SimonDB.CreDbPar("@recuserid", recuserid), SimonDB.CreDbPar("@recuserpwd", SimonUtils.EnCodeMD5(newrpwd)) };
        SimonDB.ExecuteNonQuery(@"update RecUser set recuserpwd=@recuserpwd where recuserid=@recuserid", updateparms);

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = "null";
        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 推荐人详情
    protected void RecUserDetails()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID错误"));
        }

        DataTable RecUserDT = SimonDB.DataTable(@"select * from RecUser where recuserid=@recuserid", new DbParameter[] { SimonDB.CreDbPar("@recuserid", recuserid) });
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
        jd["results"]["parentrecuserid"] = RecUserDR["ParentRecUserID"].ToString();
        jd["results"]["realname"] = RecUserDR["RealName"].ToString();
        jd["results"]["mobile"] = RecUserDR["Mobile"].ToString();
        jd["results"]["address"] = RecUserDR["Address"].ToString();
        jd["results"]["adddate"] = RecUserDR["AddDate"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    } 
    #endregion

    #region 金币变化日志(分页)
    protected void MoneyChangeLog()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string changetype = SimonUtils.Qnum("changetype");  //(非必选)金币变化类型 为空. 筛选所有的 1.只筛选增加的;  2.只筛选减少的;
        string userid = SimonUtils.Qnum("userid");  //(非必选)游戏用户ID
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
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
            DbParameter[] user_parms = { SimonDB.CreDbPar("@userid", userid), SimonDB.CreDbPar("@recuserid", recuserid) };
            if (((int)SimonDB.ExecuteScalar(@"select count(*) from TUsers where userid=@userid and recuserid=@recuserid", user_parms)) <= 0)
            {
                SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "您不是该用户的推荐人"));
            }

            lpar.Add(SimonDB.CreDbPar("@userid", userid));
            lwhere.Add("a.userid=@userid");
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
        switch (changetype)
        {
            case "1":
                lwhere.Add("ChangeMoney > 0");
                break;
            case "2":
                lwhere.Add("ChangeMoney < 0");
                break;
        }

        if (recuserid != "668899")  //此ID可以查看所有用户记录
        {
            lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));
            lwhere.Add("b.recuserid=@recuserid");
        }

        lwhere.Add("isrobot=0"); //过滤掉机器人

        string _moneychangetotalsql = @"select coalesce(sum(ChangeMoney),0) as all_total,
                                               coalesce(sum(case when ChangeMoney > 0 then ChangeMoney else 0 end),0) as increase_total,
                                               coalesce(sum(case when ChangeMoney < 0 then ChangeMoney else 0 end),0) as reduce_total
                                        from Web_MoneyChangeLog as a inner join TUsers as b on a.UserID=b.UserID {0}";
        string _countsql = @"select count(1) from Web_MoneyChangeLog as a inner join TUsers as b on a.UserID=b.UserID {0}";
        string _listsql = @"select * from (
                                    select row_number() over (order by DateTime desc) as row,
                                    b.userid,b.username,b.nickname,b.recuserid,a.startmoney,a.changemoney,a.changetype,a.datetime,a.remark
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

    #region 推荐人给自己名下用户充值（使用推荐人金币余额）
    protected void RecUserChangeUserMoney()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string userid = SimonUtils.Qnum("userid");  //游戏用户ID
        string changetype = SimonUtils.Qnum("changetype");  //修改金币类型 201.推荐人修改用户金币数-充值  202.推荐人修改用户金币数-赠送  203.推荐人修改用户金币数-扣除
        string changemoney = Request.Params["changemoney"];  //充值、扣除金币数(例如： 充值10000金币，扣除时为负值 -10000金)

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
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
        if (changetype != "201" && changetype != "202" && changetype != "203")
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "修改金币类型只能为(数字类型)： 201.推荐人修改用户金币数-充值  202.推荐人修改用户金币数-赠送  203.推荐人修改用户金币数-扣除"));
        }
        if (string.IsNullOrWhiteSpace(changemoney))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写充值或扣除金币数(整数类型,包含负值)"));
        }
        if (!SimonUtils.IsInt(changemoney))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值或扣除金币数为整数类型(包含负值)"));
        }
        if ((changetype == "201" || changetype == "202") && long.Parse(changemoney) < 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值，赠送金币时,金币数需为正数"));
        }
        if (changetype == "203" && long.Parse(changemoney) > 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "扣除金币时,金币数需为负数"));
        }

        DataTable RecUserDT = SimonDB.DataTable(@"select * from RecUser where RecUserID=@RecUserID", new DbParameter[] { SimonDB.CreDbPar("@RecUserID", recuserid) });
        if (RecUserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人不存在"));
        }
        DataRow RecUserDR = RecUserDT.Rows[0];
        if (long.Parse(changemoney) > long.Parse(RecUserDR["Coinbalance"].ToString()))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "当前推荐人金币余额不足"));
        }

        DataTable UserDT = SimonDB.DataTable(@"select a.*, b.WalletMoney from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", new DbParameter[] { SimonDB.CreDbPar("@userid", userid) });
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "游戏用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];
        if (UserDR["RecUserID"].ToString() != recuserid)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "只能给当前推荐人名下的用户充值/赠送/扣除"));
        }
        if (long.Parse(changemoney) < 0 && -long.Parse(changemoney) > long.Parse(UserDR["WalletMoney"].ToString()))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "扣除金币数不能大于用户钱包金币数"));
        }

        string Remark = "";
        switch (changetype)
        {
            case "201":
                Remark = "推荐人修改用户金币数-充值";
                break;
            case "202":
                Remark = "推荐人修改用户金币数-赠送";
                break;
            case "203":
                Remark = "推荐人修改用户金币数-扣除";
                break;
        }

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@userid", userid));
        lpar.Add(SimonDB.CreDbPar("@changemoney", changemoney));
        lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));
        //更新推荐人金币余额 (扣除推荐人金币余额，增加用户金币数；反之扣除用户金币数，增加推荐人金币余额)
        SimonDB.ExecuteNonQuery(@"update RecUser set Coinbalance=Coinbalance-@changemoney where recuserid=@recuserid", lpar.ToArray());
        //写入推荐人金币余额变化日志
        lpar.Add(SimonDB.CreDbPar("@ruRealName", RecUserDR["RealName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@ruStartMoney", RecUserDR["Coinbalance"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@ruRemark", Remark));
        SimonDB.ExecuteNonQuery(@"insert into RecUserMoneyChangeLog (RecUserID,RealName,StartMoney,ChangeMoney,Remark)
                                                             values (@RecUserID,@ruRealName,@ruStartMoney,-@ChangeMoney,@ruRemark)", lpar.ToArray());

        //更新玩家金币数
        SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@changemoney where userid=@userid", lpar.ToArray());
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

    #region 推荐人金币余额变化日志(分页)(当前推荐人)
    protected void RecUserMoneyChangeLog()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
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
            tempdic.Add("recuserid", DR["RecUserID"].ToString());
            tempdic.Add("realname", DR["RealName"].ToString());
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

    #region 推荐人名下玩家统计
    protected void TuijianPlayers()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
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
        lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));

        string _ShareProfitTotalsql = "select a.UserID,a.UserName,a.NickName,c.IsRobot,a.BankMoney,a.WalletMoney,a.RegisterTM,a.RegisterIP,b.ComName,b.RoomName,a.LastLoginTM,a.LastLoginIP,c.RecUserID,a.AllLoginTime,(CASE WHEN isnull(a.OnlineFlag, 0) > 0 THEN '在线' ELSE '不在线' END) AS OnlineStatus from  ( Web_vUserList as a left join Web_VGetUserOnline as b on b.userid = a.userid  left join  TUsers as c on c.UserID = a.UserID )where c.RecUserID = @recuserid and c.IsRobot = 0";
        string _countsql = @"select count(1) from (" + _ShareProfitTotalsql + ") as newtb";
        string _listsql = @"select * from (select row_number() over (order by LastLoginTM desc) as row,* from  (" + _ShareProfitTotalsql + ")  as newtb ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";


        DataTable _ShareProfitTotalDT = SimonDB.DataTable(_ShareProfitTotalsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ShareProfitTotalDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ShareProfitTotalDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["UserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("BankMoney", DR["BankMoney"].ToString());
            tempdic.Add("WalletMoney", DR["WalletMoney"].ToString());
            tempdic.Add("RegisterTM", DR["RegisterTM"].ToString());
            tempdic.Add("RegisterIP", DR["RegisterIP"].ToString());
            tempdic.Add("ComName", DR["ComName"].ToString());
            tempdic.Add("RoomName", DR["RoomName"].ToString());
            tempdic.Add("LastLoginTM", DR["LastLoginTM"].ToString());
            tempdic.Add("LastLoginIP", DR["LastLoginIP"].ToString());
            tempdic.Add("AllLoginTime", DR["AllLoginTime"].ToString());
            tempdic.Add("OnlineStatus", DR["OnlineStatus"].ToString());
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

    #region 分润明细（分页）【以流水统计】（按日期统计）
    protected void GetShareProfitTotal()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
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
            lwhere.Add("CollectTime >= @startdt");
        }
        else
        {
            lpar.Add(SimonDB.CreDbPar("@startdt", "1970-01-01"));
            lwhere.Add("CollectTime >= '1970-01-01'");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("CollectTime <= @enddt");
        }
        else
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", DateTime.Now.ToString()));
            lwhere.Add("CollectTime <= getdate()");
        }

        //string _ShareProfitTotalsql = "select a.UserID,b.UserName,b.NickName,a.TotalGold,a.TotalGameTime from(SELECT UserID, sum(Gold) as TotalGold, sum(GameTime) as TotalGameTime FROM DL_LogUserConsumeGold {0} ) as a left join(select * from TUsers) as b on b.UserID = a.UserID ";

        //string _countsql = @"select count(1) from (" + _ShareProfitTotalsql + ") as a";
        //string _listsql = @"select * from (select row_number() over (order by TotalGold desc) as row,* from  (" + _ShareProfitTotalsql + ")  as a ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        //string ShareProfitProportionsql = "select ShareProfitProportion from RecUser where RecUserID='" + recuserid + "' and ShareProfitType=2";


        //if (lwhere.Count > 0)
        //{
        //    string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray()) + " and UserID in (select UserID from TUsers where RecUserID='" + recuserid + "')" + " group by DestUserID, DestRecUserLevel";
        //    _ShareProfitTotalsql = string.Format(_ShareProfitTotalsql, _sqlwhere);
        //    _countsql = string.Format(_countsql, _sqlwhere);
        //    _listsql = string.Format(_listsql, _sqlwhere);
        //}
        //else
        //{
        //    _ShareProfitTotalsql = string.Format(_ShareProfitTotalsql, string.Empty + "where UserID in (select UserID from TUsers where RecUserID='" + recuserid + "')" + " group by DestUserID, DestRecUserLevel");
        //    _countsql = string.Format(_countsql, string.Empty);
        //    _listsql = string.Format(_listsql, string.Empty);
        //}
        
        string _ShareProfitTotalsql = " select a.DestUserID,b.UserName,b.NickName,a.GainEarn,a.DestRecUserLevel from (select DestUserID, SUM(ISNULL(Earn, 0)) as GainEarn, DestRecUserLevel from RecProportionEarnLog {0} ) as a left join TUsers as b on b.UserID = a.DestUserID  ";

        string _countsql = @"select count(1) from (" + _ShareProfitTotalsql + ") as a";

        string _listsql = @"select * from (select row_number() over (order by GainEarn desc) as row,* from  (" + _ShareProfitTotalsql + ")  as a ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray()) + " group by DestUserID, DestRecUserLevel";
        _ShareProfitTotalsql = string.Format(_ShareProfitTotalsql, _sqlwhere);
        _countsql = string.Format(_countsql, _sqlwhere);
        _listsql = string.Format(_listsql, _sqlwhere);


        DataTable _ShareProfitTotalDT = SimonDB.DataTable(_ShareProfitTotalsql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable ShareProfitTotalDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in ShareProfitTotalDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("UserID", DR["DestUserID"].ToString());
            tempdic.Add("UserName", DR["UserName"].ToString());
            tempdic.Add("NickName", DR["NickName"].ToString());
            tempdic.Add("GainEarn", DR["GainEarn"].ToString());
            tempdic.Add("DestRecUserLevel", DR["DestRecUserLevel"].ToString());   //收益来源等级，数字对应级别，1级为我的推广，2级为下级推广，3级为下下级推广
            resultslist.Add(tempdic);
        }

        //string LevelEarn = "select SUM(ISNULL(Earn,0)) as Level1Earn from RecProportionEarnLog";
        //string levelwhere = " where " + string.Join(" and ", lwhere.ToArray());
        

        //decimal Level1Earn = (Decimal)SimonDB.ExecuteScalar(string.Format(LevelEarn, _sqlwhere+ "and DestRecUserLevel=1"));
        //decimal Level2Earn = (Decimal)SimonDB.ExecuteScalar(string.Format(LevelEarn, _sqlwhere + "and DestRecUserLevel=2"));
        //decimal Level3Earn = (Decimal)SimonDB.ExecuteScalar(string.Format(LevelEarn, _sqlwhere + "and DestRecUserLevel=3"));

        Dictionary<string, object> jsondic = new Dictionary<string, object>();
        jsondic.Add("code", "1");
        jsondic.Add("msg", "获取数据成功");
        jsondic.Add("totalearn", ShareProfitTotalDT.Compute("sum(GainEarn)", "true").ToString());
        jsondic.Add("level1earn", ShareProfitTotalDT.Compute("sum(GainEarn)", "DestRecUserLevel=1").ToString());
        jsondic.Add("level2earn", ShareProfitTotalDT.Compute("sum(GainEarn)", "DestRecUserLevel=2").ToString());
        jsondic.Add("level3earn", ShareProfitTotalDT.Compute("sum(GainEarn)", "DestRecUserLevel=3").ToString());
        jsondic.Add("recordcount", RecordCount.ToString());
        jsondic.Add("totalpage", TotalPage.ToString());
        jsondic.Add("pagesize", pagesize);
        jsondic.Add("pageindex", pageindex);
        jsondic.Add("results", resultslist);

        SimonUtils.RespWNC(JsonMapper.ToJson(jsondic));
    }
    #endregion

    #region 获取分润金币
    protected void GetMineShareGold()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (recuserid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人ID错误"));
        }

        DataTable RecUserGoldDT = SimonDB.DataTable(@"select * from RecUserGold where recuserid=@recuserid", new DbParameter[] { SimonDB.CreDbPar("@recuserid", recuserid) });
        if (RecUserGoldDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "无数据"));
        }
        DataRow RecUserGoldDR = RecUserGoldDT.Rows[0];
        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "success";
        jd["results"] = new JsonData();
        jd["results"]["recuserid"] = RecUserGoldDR["RecUserID"].ToString();
        jd["results"]["amount"] = RecUserGoldDR["Amount"].ToString();
        jd["results"]["hadcash"] = RecUserGoldDR["HadCash"].ToString();
        jd["results"]["cancash"] = RecUserGoldDR["CanCash"].ToString();
        jd["results"]["nocash"] = RecUserGoldDR["NoCash"].ToString();

        SimonUtils.RespWNC(jd.ToJson());
    }
    #endregion

    #region 提交取款订单
    protected void PostCashOrder()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string orderGold = SimonUtils.Qnum("ordergold");

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "未登录或已超时,请重新登录"));
        }
        if (string.IsNullOrWhiteSpace(orderGold))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请填写取款金币数(大于0)"));
        }
        if (decimal.Parse(orderGold)<=0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "取款金币数需大于0"));
        }
        if (!SimonUtils.IsDecimal(orderGold))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "取款金币格式有问题"));
        }

        DataTable RecUserGoldDT = SimonDB.DataTable(@"select * from RecUserGold where RecUserID=@RecUserID", new DbParameter[] { SimonDB.CreDbPar("@RecUserID", recuserid) });
        if (RecUserGoldDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人不存在"));
        }
        DataRow RecUserGoldDR = RecUserGoldDT.Rows[0];
        decimal recusergold = Convert.ToDecimal(RecUserGoldDR["CanCash"]);
        if (decimal.Parse(orderGold) > recusergold)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "申请取款金币超过可提额度"));
        }
        if (int.Parse(RecUserGoldDR["Status"].ToString())>0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "账户状态异常，不可申请提现，请联系管理员"));
        }

        Random num = new Random();
        string OrderNumID = "TX"+ (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000+num.Next(1000000);

        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@orderGold", orderGold));
        lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));
        //更新推广员金币表余额 (扣除推广员可提金币余额)
        SimonDB.ExecuteNonQuery(@"update RecUserGold set CanCash=CanCash-@orderGold,HadCash=HadCash+@orderGold where recuserid=@recuserid", lpar.ToArray());
        //写入推广员金币订单表
        lpar.Add(SimonDB.CreDbPar("@OrderNumID", OrderNumID));
        SimonDB.ExecuteNonQuery(@"insert into RecUserCashOrder (OrderNumID,RecUserID,OrderGold) values (@OrderNumID,@recuserid,@orderGold)", lpar.ToArray());
        lpar.Add(SimonDB.CreDbPar("@beforegold", recusergold));
        lpar.Add(SimonDB.CreDbPar("@aftergold", Math.Round((decimal)recusergold - decimal.Parse(orderGold))));
        //写入推广员金币流通表
        SimonDB.ExecuteNonQuery(@"insert into RecUserCashLog (OrderNumID,RecUserID,BeforeGold,ChangeGold,AfterGold,Status) values (@OrderNumID,@recuserid,@beforegold,@orderGold,@aftergold,1)", lpar.ToArray());

        JsonData jd = new JsonData();
        jd["code"] = "1";
        jd["msg"] = "提交成功，请等待审核";
        jd["results"] = "null";

        SimonUtils.RespWNC(jd.ToJson());
    }

    #endregion

    #region 提款记录
    protected void GetCashData()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string startdt = Request.Params["startdt"];  //(非必选)时间段筛选，开始时间
        string enddt = Request.Params["enddt"];  //(非必选)时间段筛选，结束时间

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
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
            lwhere.Add("AddTime >= @startdt");
        }
        if (!string.IsNullOrWhiteSpace(enddt) && SimonUtils.IsStringDate(enddt))
        {
            lpar.Add(SimonDB.CreDbPar("@enddt", enddt));
            lwhere.Add("AddTime <= @enddt");
        }
        else
        {
            lwhere.Add("datediff(DD,AddTime,getdate())=0 ");
        }

        string _CashDatasql = "select * from RecUserCashLog {0}";

        string _countsql = @"select count(1) from RecUserCashLog {0}";
        string _listsql = @"select * from (select row_number() over (order by AddTime desc) as row,* from  RecUserCashLog {0} ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (lwhere.Count > 0)
        {
            string _sqlwhere = " where " + string.Join(" and ", lwhere.ToArray());
            _CashDatasql = string.Format(_CashDatasql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        else
        {
            _CashDatasql = string.Format(_CashDatasql, string.Empty );
            _countsql = string.Format(_countsql, string.Empty);
            _listsql = string.Format(_listsql, string.Empty);
        }

        DataTable CashDataDT = SimonDB.DataTable(_CashDatasql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable CashDataListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in CashDataListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("OrderNumID", DR["OrderNumID"].ToString());
            tempdic.Add("RecUserID", DR["RecUserID"].ToString());
            tempdic.Add("BeforeGold", DR["BeforeGold"].ToString());
            tempdic.Add("ChangeGold", DR["ChangeGold"].ToString());
            tempdic.Add("AfterGold", DR["AfterGold"].ToString());
            tempdic.Add("Status", DR["Status"].ToString());
            tempdic.Add("AddTime", DR["AddTime"].ToString());
            tempdic.Add("UpdateTime", DR["UpdateTime"].ToString());
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

    #region 多级推荐人列表
    protected void RecUserList()
    {
        CheckRecUserSign();
        string token = Request.Params["token"];  //登录验证token
        string pageindex = SimonUtils.Qnum("pageindex");  //页码
        string pagesize = SimonUtils.Qnum("pagesize");  //每页记录条数
        string reclevel = SimonUtils.Qnum("reclevel");  //推荐人级别（1为1级推荐员，2为2级推荐员）

        string recuserid = "0";
        if (!CheckRecUserLogin(token, out recuserid))
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
        if (string.IsNullOrWhiteSpace(reclevel))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "推荐人级别不可为空"));
        }

        List<DbParameter> lpar = new List<DbParameter>();
        List<string> lwhere = new List<string>();

        if (recuserid.Length > 0)
        {
            lpar.Add(SimonDB.CreDbPar("@recuserid", recuserid));
            lwhere.Add("recuserid=@recuserid");
        }

        string _reclistSql= "select a.RecUserID,b.Amount,a.ShareProfitProportion,(select SecondLevelProportion from RecLevelProportion) as Proportion,c.UserCount from (select RecUserID, ShareProfitProportion from RecUser {0}) as a  left join  RecUserGold as b on b.RecUserID = a.RecUserID left join (select recuserid, ISNULL(COUNT(UserID),0) as UserCount from TUsers group by RecUserID) as c on c.RecUserID=a.RecUserID";
        string _countsql = @"select count(1) from ("+ _reclistSql + ") as a";
        string _listsql = @"select * from (select row_number() over (order by Amount desc) as row,* from  (" + _reclistSql + ") as a ) as tb where row between (@pageindex-1)*@pagesize+1 and (@pageindex-1)*@pagesize+@pagesize";

        if (reclevel == "1")
        {
            string _sqlwhere = "where ParentRecUserID = @recuserid";
            _reclistSql = string.Format(_reclistSql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }
        if (reclevel == "2")
        {
            string _sqlwhere = "where ParentRecUserID in(select RecUserID from RecUser where ParentRecUserID = @recuserid)";
            _reclistSql = string.Format(_reclistSql, _sqlwhere);
            _countsql = string.Format(_countsql, _sqlwhere);
            _listsql = string.Format(_listsql, _sqlwhere);
        }

        DataTable _RecListDT = SimonDB.DataTable(_reclistSql, lpar.ToArray()); //统计计算
        int RecordCount = (int)SimonDB.ExecuteScalar(_countsql, lpar.ToArray());
        int TotalPage = SimonPager.GetTotalPage(RecordCount, int.Parse(pagesize));
        if (pageindex == "0") pageindex = "1";  //默认第1页
        if (pagesize == "0") pagesize = "10";  //默认每页10条
        if (int.Parse(pageindex) > TotalPage) pageindex = TotalPage.ToString();

        lpar.Add(SimonDB.CreDbPar("@pageindex", pageindex));
        lpar.Add(SimonDB.CreDbPar("@pagesize", pagesize));

        DataTable RecListDT = SimonDB.DataTable(_listsql, lpar.ToArray());
        List<Dictionary<string, string>> resultslist = new List<Dictionary<string, string>>();
        foreach (DataRow DR in RecListDT.Rows)
        {
            Dictionary<string, string> tempdic = new Dictionary<string, string>();
            tempdic.Add("RecUserID", DR["RecUserID"].ToString());
            tempdic.Add("Amount", DR["Amount"].ToString());
           // tempdic.Add("ShareProfitProportion", DR["ShareProfitProportion"].ToString());
          //  tempdic.Add("Proportion", DR["Proportion"].ToString());
            tempdic.Add("UserCount", DR["UserCount"].ToString());
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

}