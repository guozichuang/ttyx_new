/**
 * Created by SuperMan on 2016/10/18.
 */

/*
* 鏅鸿兘鏈烘祻瑙堝櫒鐗堟湰淇℃伅:
*
*/
var browser = {
    versions: function () {
        var u = navigator.userAgent, app = navigator.appVersion;
        return {//绉诲姩缁堢娴忚鍣ㄧ増鏈俊鎭�
            trident: u.indexOf('Trident') > -1, //IE鍐呮牳
            presto: u.indexOf('Presto') > -1, //opera鍐呮牳
            webKit: u.indexOf('AppleWebKit') > -1, //鑻规灉銆佽胺姝屽唴鏍�
            gecko: u.indexOf('Gecko') > -1 && u.indexOf('KHTML') == -1, //鐏嫄鍐呮牳
            mobile: !!u.match(/AppleWebKit.*Mobile.*/) || u.indexOf('iPad') > -1, //鏄惁涓虹Щ鍔ㄧ粓绔�
            ios: !!u.match(/\(i[^;]+;( U;)? CPU.+Mac OS X/), //ios缁堢
            android: u.indexOf('Android') > -1 || u.indexOf('Linux') > -1, //android缁堢鎴栬€卽c娴忚鍣�
            iPhone: u.indexOf('iPhone') > -1, //鏄惁涓篿Phone鎴栬€匭QHD娴忚鍣�
            iPad: u.indexOf('iPad') > -1, //鏄惁iPad
            webApp: u.indexOf('Safari') == -1 //鏄惁web搴旇绋嬪簭锛屾病鏈夊ご閮ㄤ笌搴曢儴
        };
    }(),
    language: (navigator.browserLanguage || navigator.language).toLowerCase()
};


(function () {
    var num = 1 / window.devicePixelRatio;

    document.write('<meta name="viewport"content="width=device-width, user-scalable=no, initial-scale=' + num + ', maximum-scale=' + num + ', minimum-scale=' + num + '">');
    var fontsize = document.documentElement.clientWidth / 10;
    document.querySelector('html').style.fontSize = fontsize + 'px';
    var container = document.querySelector('#container');

    if (browser.versions.iPad) {
        document.querySelector('html').style.fontSize = 124 + 'px';
    }
    if (/Android|webOS|iPhone|iPod|ipad|BlackBerry/i.test(navigator.userAgent)) {
        // alert('绉诲姩绔�');
    } else {
        document.querySelector('html').style.fontSize = 75 + 'px';
    }
})();
