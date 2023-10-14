ps_3_0
def c0, 1, 2, 3, 4
dcl_texcoord v0.yzw
dp2add oC0.x, v0.yz, v0.zw, c0.xx
mov oC0.yzw, c0.yzw
