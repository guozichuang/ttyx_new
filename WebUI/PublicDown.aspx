<%@ Page Language="C#" AutoEventWireup="true" CodeFile="PublicDown.aspx.cs" Inherits="PublicDown" %>

<!doctype html>
<html lang="en">
<head>
	<meta charset="UTF-8">
    <meta name="viewport" content="width=device-width,initial-scale=1.0, minimum-scale=1.0, maximum-scale=1.0, user-scalable=no"/>
    
    <link rel="shortcut icon" href="../Img/qilingIcon.png">
	<title>麒陵麻将</title>
</head>
<body>
    <div>
        <img src="Img/Bg.png" style="width:100%;height:100%;position:absolute;z-index:-1;" />
	<input  id="btn" type="button"  style="background:url(../Img/click.png); width:360px; height:160px; border:none; position:absolute; bottom:450px; left:480px; ">
    </div>
    <script src="js/common.js"></script>
    <script src="js/jquery.min.js"></script>
	<script>

        /*判断微信内置浏览器中打开*/
        function is_weixin() {
            var ua = navigator.userAgent.toLowerCase();
            if (ua.match(/MicroMessenger/i) == "micromessenger") {
                return true;
            } else {
                return false;
            }
        }

        var isWeixin = is_weixin();
        var winHeight = typeof window.innerHeight != 'undefined' ? window.innerHeight : document.documentElement.clientHeight;
        var weixinTip = $('<div id="weixinTip"><p><img src="../Img/live_weixin.png" alt="微信打开" style="width: 100%"/></p></div>');

        if (isWeixin) {
            $('.activity').hide();
            $("body").append(weixinTip);
        }
        $("#weixinTip").css({
            "position": "fixed",
            "left": "0",
            "top": "0",
            "height": winHeight,
            "width": "100%",
            "z-index": "99999999999",
            "background-color": "rgba(0,0,0,0.8)",
            "filter": "alpha(opacity=80)",
        });
        $("#weixinTip p").css({
            "text-align": "center",
            "margin-top": "10%",
            "padding-left": "5%",
            "padding-right": "5%"
        });


	    function GetQueryString(name)
	    {
	        var reg = new RegExp("(^|&)"+ name +"=([^&]*)(&|$)");
	        var r = window.location.search.substr(1).match(reg);
	        if(r!=null)return  unescape(r[2]); return null;
	    }

		var btn = document.getElementById('btn');
		
		btn.onclick = function () {
		    jump('kingcards://www.njkingsoft.com:8888/open?roomnum='+GetQueryString("roomnum"));
		};
		
		function  GetMobelType()  {                
			var  browser  =   {                    
				versions:   function()  {                        
					var  u  =  window.navigator.userAgent;    
					console.log(u);  //Safari浏览器 Mozilla/5.0 (Macintosh; Intel Mac OS X 10_13_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/11.1.1 Safari/605.1.15                  
					return  {                            
						trident:  u.indexOf('Trident')  >  -1, //IE内核
						presto:  u.indexOf('Presto')  >  -1, //opera内核
						Alipay:  u.indexOf('Alipay')  >  -1, //支付宝
						webKit:  u.indexOf('AppleWebKit')  >  -1, //苹果、谷歌内核
						gecko:  u.indexOf('Gecko')  >  -1  &&  u.indexOf('KHTML')  ==  -1, //火狐内核
						mobile:  !!u.match(/AppleWebKit.*Mobile.*/), //是否为移动终端
						ios:  !!u.match(/\(i[^;]+;( U;)? CPU.+Mac OS X/), //ios终端
						android:  u.indexOf('Android')  >  -1  ||  u.indexOf('Linux')  >  -1, //android终端或者uc浏览器
						iPhone:  u.indexOf('iPhone')  >  -1  ||  u.indexOf('Mac')  >  -1, //是否为iPhone或者安卓QQ浏览器
						//iPhone: u.match(/iphone|ipod|ipad/),//
						iPad:  u.indexOf('iPad')  >  -1, //是否为iPad
						webApp:  u.indexOf('Safari')  ==  -1, //是否为web应用程序，没有头部与底部
						weixin:  u.indexOf('MicroMessenger')  >  -1, //是否为微信浏览器
						qq: u.match(/\sQQ/i) !== null, //是否QQ
						Safari:  u.indexOf('Safari')  >  -1,
						  ///Safari浏览器,
					};                    
				}()                
			};                
			return  browser.versions;            
		}
		
		
		function jump(myurl) { 
			var timeout = 2300, timer = null;
			if(GetMobelType().weixin) {
				// 微信浏览器不支持跳转
				// 可以显示提示在其他浏览器打开
			} else {
				var startTime = Date.now();
				if(GetMobelType().android) {
					var ifr = document.createElement('iframe');
					ifr.src = myurl;//这里是唤起App的协议，有Android可爱的同事提供
					ifr.style.display = 'none';
					document.body.appendChild(ifr);
					timer = setTimeout(function() {
						var endTime = Date.now();
						if(!startTime || endTime - startTime < timeout + 300) {
							document.body.removeChild(ifr);
							//window.open("唤起失败跳转的链接");
							window.open("https://fir.im/wzqpw");
						}
					}, timeout);
				}
				if(GetMobelType().ios || GetMobelType().iPhone || GetMobelType().iPad) {
					if(GetMobelType.qq) { 
					// ios的苹果浏览器
					// 提示在浏览器打开的蒙板
					} else {
						/*var ifr = document.createElement("iframe");
						ifr.src = myurl;
						ifr.style.display = "none";*/ // iOS9+不支持iframe唤起app
						window.location.href = myurl; //唤起协议，由iOS小哥哥提供
						//document.body.appendChild(ifr);
						
						timer = setTimeout(function() {
							// window.location.href = "ios下载的链接";
						    window.location.href = "https://fir.im/wzqpw";
						}, timeout);
					};
				}
			}
		}
	</script>
</body>
</html>
