<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:fo="http://www.w3.org/1999/XSL/Format">

    <!--
      ============================================================================
      XSLT 1.0 port of deepseek_xml_20261003_4d888a.xslt (Altova XSLT 2.0 fixture).

      Why a port exists:
        * The original is declared version="2.0"; the managed BCL route uses
          XslCompiledTransform, which implements XSLT 1.0 only.
        * The original calls altova:evaluate(), an Altova COM extension. AltovaGen
          is deliberately 100% COM-free, so the extension cannot be honoured.
        * The original computes sum(//items/item/@price * //items/item/quantity),
          which is XSLT 2.0 sequence arithmetic. XPath 1.0 has no such construct,
          so the total is accumulated by a recursive named template.

      What it produces:
        A complete XSL-FO 1.1 instance (fo:root / fo:layout-master-set /
        fo:page-sequence / fo:flow / fo:table) - the FO document that an FO
        processor such as Apache FOP renders to PDF. AltovaGen stops at FO:
        it does not embed an FO renderer.
      ============================================================================
    -->

    <!-- Выход — XSL-FO (XML) -->
    <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

    <!-- ======================= ГЛАВНЫЙ ШАБЛОН ======================= -->
    <xsl:template match="/">
        <fo:root>
            <!-- Определение макета страницы -->
            <fo:layout-master-set>
                <fo:simple-page-master master-name="A4"
                    page-height="29.7cm"
                    page-width="21cm"
                    margin-top="2cm"
                    margin-bottom="2cm"
                    margin-left="2cm"
                    margin-right="2cm">
                    <fo:region-body margin-bottom="1.5cm"/>
                    <fo:region-after extent="1cm"/>
                </fo:simple-page-master>
            </fo:layout-master-set>

            <!-- Страница -->
            <fo:page-sequence master-reference="A4">

                <!-- Нижний колонтитул -->
                <fo:static-content flow-name="xsl-region-after">
                    <fo:block text-align="center" font-size="9pt" color="#555555">
                        <xsl:value-of select="//footer"/>
                        <xsl:text> — стр. </xsl:text>
                        <fo:page-number/>
                    </fo:block>
                </fo:static-content>

                <!-- Основной поток -->
                <fo:flow flow-name="xsl-region-body">

                    <!-- Заголовок -->
                    <fo:block font-size="20pt" font-weight="bold"
                              text-align="center" space-after="3mm">
                        <xsl:value-of select="//document/title"/>
                    </fo:block>

                    <!-- Подзаголовок -->
                    <fo:block font-size="12pt" font-style="italic"
                              text-align="center" color="#333333" space-after="2mm">
                        <xsl:value-of select="//document/subtitle"/>
                    </fo:block>

                    <fo:block font-size="9pt" text-align="center"
                              color="#777777" space-after="8mm">
                        <xsl:text>Сформировано: </xsl:text>
                        <xsl:value-of select="//document/generated"/>
                    </fo:block>

                    <!-- ===== Замена altova:evaluate() =====
                         В оригинале:
                             <xsl:variable name="expr" select="string(//dynamic-expression)"/>
                             <xsl:variable name="result" select="altova:evaluate($expr)"/>
                         altova:evaluate() вычисляет XPath относительно текущего узла,
                         то есть относительно самого элемента dynamic-expression.
                         Для выражения "../title/text()" эквивалентный стандартный
                         XPath 1.0 — $dynamicContext/../title/text().
                         Произвольные выражения требуют XPath 2.0-движка (Saxon). -->
                    <fo:block font-size="11pt" space-after="6mm"
                              background-color="#F0F4FF" padding="3mm">
                        <fo:inline font-weight="bold">
                            <xsl:text>Результат altova:evaluate(): </xsl:text>
                        </fo:inline>
                        <xsl:variable name="dynamicContext" select="//dynamic-expression"/>
                        <xsl:variable name="result" select="$dynamicContext/../title/text()"/>
                        <xsl:value-of select="$result"/>
                    </fo:block>

                    <!-- ===== Таблица позиций ===== -->
                    <fo:table table-layout="fixed" width="100%"
                              border="0.5pt solid #999999">
                        <fo:table-column column-width="10%"/>
                        <fo:table-column column-width="40%"/>
                        <fo:table-column column-width="15%"/>
                        <fo:table-column column-width="15%"/>
                        <fo:table-column column-width="20%"/>

                        <fo:table-header background-color="#DDDDDD">
                            <fo:table-row font-weight="bold">
                                <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                    <fo:block>ID</fo:block>
                                </fo:table-cell>
                                <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                    <fo:block>Наименование</fo:block>
                                </fo:table-cell>
                                <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                    <fo:block text-align="right">Кол-во</fo:block>
                                </fo:table-cell>
                                <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                    <fo:block text-align="right">Цена, руб.</fo:block>
                                </fo:table-cell>
                                <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                    <fo:block text-align="right">Сумма, руб.</fo:block>
                                </fo:table-cell>
                            </fo:table-row>
                        </fo:table-header>

                        <fo:table-body>
                            <xsl:for-each select="//items/item">
                                <fo:table-row>
                                    <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                        <fo:block><xsl:value-of select="@id"/></fo:block>
                                    </fo:table-cell>
                                    <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                        <fo:block><xsl:value-of select="name"/></fo:block>
                                    </fo:table-cell>
                                    <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                        <fo:block text-align="right">
                                            <xsl:value-of select="quantity"/>
                                        </fo:block>
                                    </fo:table-cell>
                                    <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                        <fo:block text-align="right">
                                            <xsl:value-of select="@price"/>
                                        </fo:block>
                                    </fo:table-cell>
                                    <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                        <fo:block text-align="right">
                                            <xsl:value-of select="@price * quantity"/>
                                        </fo:block>
                                    </fo:table-cell>
                                </fo:table-row>
                            </xsl:for-each>
                        </fo:table-body>

                        <fo:table-footer background-color="#F0F0F0">
                            <fo:table-row font-weight="bold">
                                <fo:table-cell number-columns-spanned="4"
                                               border="0.5pt solid #999999"
                                               padding="2mm">
                                    <fo:block text-align="right">Итого:</fo:block>
                                </fo:table-cell>
                                <fo:table-cell border="0.5pt solid #999999" padding="2mm">
                                    <fo:block text-align="right">
                                        <!-- XSLT 2.0: sum(//items/item/@price * //items/item/quantity)
                                             XSLT 1.0: последовательное накопление -->
                                        <xsl:call-template name="sumLineTotals">
                                            <xsl:with-param name="items" select="//items/item"/>
                                        </xsl:call-template>
                                    </fo:block>
                                </fo:table-cell>
                            </fo:table-row>
                        </fo:table-footer>
                    </fo:table>

                    <!-- Описания позиций -->
                    <fo:block space-before="8mm" font-size="11pt" font-weight="bold">
                        Комментарии по позициям:
                    </fo:block>
                    <xsl:for-each select="//items/item">
                        <fo:block font-size="10pt" space-after="2mm">
                            <fo:inline font-weight="bold">
                                <xsl:value-of select="name"/>
                            </fo:inline>
                            <xsl:text>: </xsl:text>
                            <xsl:value-of select="description"/>
                        </fo:block>
                    </xsl:for-each>

                </fo:flow>
            </fo:page-sequence>
        </fo:root>
    </xsl:template>

    <!-- ======================= НАКОПЛЕНИЕ ИТОГА =======================
         Сумма price * quantity по всем позициям средствами XPath 1.0. -->
    <xsl:template name="sumLineTotals">
        <xsl:param name="items"/>
        <xsl:param name="acc" select="0"/>
        <xsl:choose>
            <xsl:when test="count($items) = 0">
                <xsl:value-of select="$acc"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:call-template name="sumLineTotals">
                    <xsl:with-param name="items" select="$items[position() &gt; 1]"/>
                    <xsl:with-param name="acc"
                                    select="$acc + $items[1]/@price * $items[1]/quantity"/>
                </xsl:call-template>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

</xsl:stylesheet>
