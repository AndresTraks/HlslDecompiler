ps_3_0
def c0, 1, 0, 2, 0
dcl_texcoord v0.xz
mov oC0.x, -v0_abs.z
mad oC0.yzw, v0.xxx, c0.xyy, c0.yxz
