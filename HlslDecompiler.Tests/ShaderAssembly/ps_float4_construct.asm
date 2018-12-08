ps_3_0
def c0, 1, 0, 2, 0
dcl_texcoord v0
mov oC0, v0
mul oC1, c0.xxxy, v0.xyzx
mad oC2, v0.xyxx, c0.xxyy, c0.yyyx
mad oC3, v0.x, c0.xyyy, c0.yyxz
