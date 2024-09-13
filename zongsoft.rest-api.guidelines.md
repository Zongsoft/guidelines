## 前言

根据 [**REST** 成熟度模型](https://martinfowler.com/articles/richardsonMaturityModel.html "Richardson Maturity Model")，本规范基于 `Level 2` 等级。

![Glory of REST](https://martinfowler.com/articles/images/richardsonMaturityModel/overview.png "Glory of REST")


## 设计概要

**REST**ful _API_ 是对资源的表述，通过 _HTTP_ 协议相关要素表达对资源访问的语义化表征。我们设计一个 **REST**ful 的 _API_ 通常有如下步骤：


### 1. 资源定义

识别出业务领域中的资源，它是对所提供服务进行分解、组合后的一个被命名的抽象概念。资源通常是以名词定义的，应避免使用动词来表示资源。


### 2. 标识资源

资源定义好后，需要采用 _URI_ 来标识资源，一个资源可能有多种 _URI_ 标识，但是一个 _URI_ 标识只可能对应一个资源或资源集。

> 譬如 `任务` 和 `我的任务`
> - 任务 `/tasks`
>
> - 我的任务 _(假设我的用户编号是 `100`)_
>   - `/tasks/mine`
>   - `/users/100/tasks`


#### 命名规则

对于多单词的资源名建议采用蛇形命名风格(_短横线分隔符_)。

关于资源名是单数还是复数的问题，应遵循资源所蕴含的数量语义，在实际技术实现中如果不能确保每个资源的准确含义，那应该尽量统一单复数命名 _(切勿单复数形式混搭)_。


### 3. 方法映射

- 使用 **H**TTP 方法来映射资源的操作 _(CRUD)_
- 使用 **H**TTP 头来承载必要的请求/响应的元数据
- 使用 **H**TTP 状态码来表示服务的响应状态

方法 | 安全性 | 幂等性 | 说明
:---:|:-----:|:-----:|:----
GET         | ✔ | ✔ | 获取资源
PUT         | ✘ | ✔ | 完整更新（_如果不存在则新增_）
POST        | ✘ | ✘ | 创建资源（_以及未符合其他方法语义的操作_）
PATCH       | ✘ | ✘ | 部分更新
DELETE      | ✘ | ✔ | 删除资源

> - 安全性(**S**afety)：操作不会对资源产生副作用，不会修改资源。
> - 幂等性(**I**dempotent)：执行一次和重复执行多次，结果是一样的。


在某些情况下不是所有操作都能恰如其分的映射到 _HTTP_ 方法，我们应将操作目标抽象成一种子资源，譬如禁用某种资源的操作，就可以将被禁用的资源作为其子资源看待。

- 禁用调度器：
    - `[PUT] /schedulers/disabled/{id}`
- 启用调度器
    - `[DELETE] /schedulers/disabled/{id}`
- 获取被禁用的调度器集
    - `[GET] /schedulers/disabled`

这个世界总是充满意外的惊喜，如果抽象子资源的方式不可行，也可以采用 `URI/动词` 的组合方式，仍以上面操作为例：

- 禁用调度器
    - `[POST] /schedulers/{id}/disable`
- 启用调度器
    - `[POST] /schedulers/{id}/enable`


## 版本标识

因为业务调整导致 _API_ 的参数和结果变化，造成兼容性被破坏，这时需要进行 _API_ 版本标识，版本标识大致有如下三种方法。

1. 采用自定义的 _HTTP_ 头：
```http
GET api.zongsoft.com/users/100
API-Version: 1.0
```

2. 采用 _HTTP_ 的 **A**ccept 头：
```http
GET api.zongsoft.com/users/100
Accept: application/vnd.v1+json
```

3. 将 _API_ 版本信息在 _URL_ 的路径中：
```http
GET api.zongsoft.com/v1/users/100
```

> 💡 建议采用 `方法一`，所有请求默认加上版本标识以便后续向下兼容。


## 状态码

**REST**ful 服务采用 _HTTP_ 状态码指定方法的执行结果。

状态码 | 描述说明
------|----------
`200` **O**K                 | 执行成功。
`201` **C**reated            | 资源创建成功，应该返回 **L**ocation 响应头来提供新创建资源的URL地址。
`202` **A**ccepted           | 服务端已经接受了请求，但是并未处理完成，适用于一些异步操作，譬如批量导出文件等。
`204` **N**o **C**ontent         | 执行成功，但是不会在响应内容无数据。
`400` **B**ad **R**equest        | 客户端请求错误，客户端应该根据响应内容中的错误描述来修改请求，然后才能再次发送。
`401` **U**nauthorized       | 客户端未提供授权信息。
`403` **F**orbidden          | 客户端无权访问 _（客户端已经提供了授权信息，但是权限不够）_。
`404` **N**ot **F**ound          | 客户端请求的资源不存在。
`405` **M**ethod **N**ot **A**llowed | 客户端使用了不被允许的方法。比如某个操作只允许 **P**OST 但是客户端采用了 **P**UT。
`406` **N**ot **A**cceptable     | 客户端发送的 **A**ccept 不被支持。<br/>比如客户端发送了 `application/xml`，但是服务器只支持 `application/json`。
`409` **C**onflict           | 客户端提交的数据过于陈旧，和服务端的存在冲突，需要客户端重新获取最新的资源再发起请求。
`415` **U**nsupported **M**edia **T**ype | 客户端发送的 **C**ontent-**T**ype 不被支持。<br/>比如客户端发送了`application/xml`，但是服务器只支持 `application/json`。
`422` **U**nprocessable **E**ntity   | 请求实体的格式和语法正确，但由于语义错误导致服务器无法处理。
`429` **T**oo **M**any **R**equests      | 客户端在指定的时间内发送了太多次数的请求。
`500` **I**nternal **S**erver **E**rror  | 服务器遇见了未知的内部错误。
`501` **N**ot **I**mplemented        | 服务器还未实现此项功能。
`503` **S**ervice **U**navailable    | 服务器繁忙，无法处理客户端的请求。

> 参考资料：
> - [《HTTP 状态码说明》](https://developer.mozilla.org/zh-CN/docs/Web/HTTP/Status)
> - [《HTTP 协议标准 1.1》](https://tools.ietf.org/html/rfc2616#section-10)

## 参数

### 公共参数

公共参数指通过 _URL_ 中查询字符串指定的部分。

- 分页参数：`page`，语法：`PageIndex[|PageSize]` 或 `PageIndex[/PageSize]`
    > - `PageIndex` 和 `PageSize` 的值均为正整数，其中 `PageSize` 可忽略 _（缺省表示系统默认值）_。
    > - 禁用分页：`page=0`或`page=disabled`，由于禁用分页可能会因为查询数据量过大导致系统出错，默认应关闭该项功能。

- 排序参数：`sort`，语法：`(+|-)field[,...]`
    > - 字段前通过一个 `+`加号 或 `-`减号来表示升序或降序；
    > - 如果省略升降序符号(即`+`或`-`号)，缺省由后台服务自行裁决。

### 查询条件

通常 **G**ET 和 **D**ELETE 方法应支持过滤条件，其条件字段名与字段值中间用 `:` 分隔，多个条件项用 `+` 号连接。其字段值除支持常规的数字、字符、布尔、日期时间类型外，还支持 _集合_ 与 _区间_ 两种类型。

- 集合类型（即 `IN` 操作符）：元素间采用逗号(`,`)分隔。
- 区间类型（即 `Between` 操作符）：起止元素间采用波浪线(`~`)分隔，起止值可以缺少任意一个，缺失项使用星号(`*`)占位，支持数字或日期格式。
    > 对于日期时间类型，支持日期时间范围函数，代码实现请参考 [_**Z**ongsoft.**D**ata.**R**ange+**T**iming_](https://github.com/Zongsoft/framework/blob/master/Zongsoft.Core/src/Data/Range.cs#L213) 类的定义。

#### 日期时间范围函数

- 今天：`today()`
- 明天：`tomorrow()`
- 昨天：`yesterday()`
- 本周：`thisweek()`
- 上周：`lastweek()`
- 本月：`thismonth()`
- 上月：`lastmonth()`
- 今年：`thisyear()`
- 去年：`lastyear()`

-----

- 某年：`year(yyyy)`
	> 参数 `yyyy` 为一个四位数的年份值，譬如：`1998`、`2000`、`2024` 等。
	> - `year(1979)` 表示 `1979-01-01` 至 `1979-12-31 23:59:59.999` 时间段。

- 某月：`month(yyyy,M)`
	> 参数 `yyyy` 为一个四位数的年份值；
	> 参数 `M` 为 `1` 至 `12` 的月份数。
	> - `month(2018,2)` 表示 `2018-02-01` 至 `2018-02-28 23:59:59.999` 时间段；
	> - `month(2020,12)` 表示 `2020-12-01` 至 `2020-12-31 23:59:59.999` 时间段。

- 某日：`day(yyyy,M,d)`
	> 参数 `yyyy` 为一个四位数的年份值；
	> 参数 `M` 为 `1` 至 `12` 的月份数；
	> 参数 `d` 为 `1` 至 `31` 的天数。
	> - `day(2020,1,20)` 表示 `2020-01-20` 至 `2020-01-20 23:59:59.999` 时间段；
	> - `day(2020,12,31)` 表示 `2020-12-31` 至 `2020-12-31 23:59:59.999` 时间段。

-----

- 最近以来：Last(`x`**U**)
	> 表示自从前的某个时间点距今以来的时间段。
	> - 参数分为 *数值* 与 *单位字符* 两部分，_单位字符_ 区分大小写，其定义如下：
		> - `y` ：**年**
		> - `M` ：**月**
		> - `d` ：**天**
		> - `h` ：**时**
		> - `m` ：**分**
		> - `s` ：**秒**

	> 示例：
	> - `5`年以内：`last(5y)`
	> - `5`天以内：`last(5d)`
	> - `5`个月以内：`last(5M)`
	> - `5`小时以内：`last(5h)`
	> - `5`分钟以内：`last(5m)`
	> - `5`秒钟以内：`last(5s)`

	> 🚨 注意：参数中的 _数值_ 与 _单位字符_ 之间不能有空格。

-----

- 多久之前：ago(`x`**U**)
	> 表示距离某个时间点之前的时间段。
	> - 参数分为 *数值* 与 *单位字符* 两部分，_单位字符_ 区分大小写，其定义如下：
		> - `y` ：**年**
		> - `M` ：**月**
		> - `d` ：**天**
		> - `h` ：**时**
		> - `m` ：**分**
		> - `s` ：**秒**

	> 示例：
	> - `5`年前：`ago(5y)`
	> - `5`天前：`ago(5d)`
	> - `5`个月前：`ago(5M)`
	> - `5`小时前：`ago(5h)`
	> - `5`分钟前：`ago(5m)`
	> - `5`秒钟前：`ago(5s)`

	> 🚨 注意：参数中的 _数值_ 与 _单位字符_ 之间不能有空格。

### 示例

#### 获取用户集

- 条件：
	- _**S**tatus 等于`1`或`3`_ **并且**
	- _**G**rade 介于`1`至`5`之间_ **并且**
	- _**B**irthday 为 `90` 年代及之后_ **并且**
	- _**C**reation**T**ime 为`今年`内_ **并且**
	- _**M**odification**T**ime 介于 `2024-09-01` 至 `2024-09-20 18:30:59` 之间_；
- 分页：按每页 `10` 条记录进行分页操作，并返回第 `2` 页的数据；
- 排序：_**C**reation **倒序**_，_**A**ge **正序**_，_**N**ame **正序**_。

> 💡 提示：以下 **G**ET 和 **P**OST 两种查询请求效果是等价的。

##### GET 查询请求

通过请求的 *URL* 路径传递 _条件_ 参数，通过查询字符串传递 _分页_ 与 _排序_ 设置。

```curl
GET /users/status:1,3+grade:1~5
	+Birthday:1990-1-1~*
	+CreationTime:thisyear
	+ModificationTime:2024-09-01~2024-09-20T18:30:59
	?page=2|10&sort=-creation,+age,name
```

> 💡 提示：请求路径与查询参数名均不区分大小写。

##### POST 查询请求

通过请求的包体(_**B**ody_)传递 _条件_ 参数 _(JSON格式)_，通过查询字符串传递 _分页_ 与 _排序_ 设置。

```http
POST /users/query?page=2|10&sort=-creation,age,name

{
    "status": "1,3",
    "GRADE": "1~5",
	"Birthday": "1990-1-1~",
    "CreationTime": "thisyear()",
	"modificationTime": "2024-09-01~2024-09-20T18:30:59"
}
```

> 💡 提示：请求路径与查询参数名均不区分大小写，请求内容的 _JSON_ 属性名亦不区分大小写。

> 💡 提示：**D**ELETE 删除方法同时支持路径和内容两种方式传递条件。

## 公共头

- `X-Json-Behaviors`
    > 指定返回的 _JSON_ 的选项，支持项有：`ignores` 和 `casing`，选项之间采用 `;` 分号分隔，譬如：`ignores:null,empty; casing:camel`
    > - `ignores:null` 忽略返回的 _JSON_ 中值为空(`null`)的元素；
    > - `ignores:empty` 忽略返回的 _JSON_ 中值为空集合的元素；
    > - `ignores:default` 忽略返回的 _JSON_ 中值为空(`null`)或数字为零、布尔值为假`false`的元素；
    > - `casing:camel` 指示返回的 _JSON_ 元素的命名方式为小驼峰 `camel` 模式；
    > - `casing:pascal` 指示返回的 _JSON_ 元素的命名方式为大驼峰(帕斯卡) `Pascal` 模式。

- `X-Data-Schema`
    > 指定当前操作的数据模式，有关数据模式的详细定义请参考 [_**Z**ongsoft.**D**ata_](https://github.com/Zongsoft/framework/blob/master/Zongsoft.Data) 项目文档。


## 响应内容

响应内容应该简洁直白，避免无谓的嵌套结构。

### 分页信息

对于查询请求，如果没有指定 `page` 参数则默认进行分页处理，名为 `X-Pagination` 的响应头包含结果的分页信息。
> 譬如响应分页头的内容为 `1/10(190)`，则：
> - 其中 `1` 表示页号 _(从1开始)_，即返回的结果为第 `1` 页；
> - 其中 `10` 表示分页总数，即满足条件的数据总共有 `10` 页；
> - 其中 `190` 表示记录总数，即满足条件的数据共有 `190` 条记录。

## 错误响应

当操作执行失败，仅凭 *HTTP* 状态码并不足以表达失败的原因，因此当 *HTTP* 状态码值不为 `2xx` 段时必须定义一套表达失败信息的结构。

```json
{
    "type":"InvalidOperation",
    "title":"当前状态下不能执行XXX操作。",
    "detail":"更多详细的错误信息。",

    "errors":
    [
        "Name":{
            "type":"ArgumentNull",
            "message":"指定的某某名称不能为空。"
        },
        "CreationTime":{
            "type":"ArgumentOutOfRange",
            "message":"指定的创建时间超出范围。"
        }
    ]
}
```

> 注：如果错误未限定具体字段，则其中 `errors` 属性可以缺失。

> 可以根据实际情况对上述 [RFC7807 Problem Details](https://tools.ietf.org/html/rfc7807) 规范的响应结构进行适当调整。



## 参考：

1. [《Microsoft REST API Guidelines》](https://github.com/microsoft/api-guidelines)
2. [《API Design Patterns And Use Cases》](https://github.com/paypal/api-standards/blob/master/patterns.md)
