ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
frc oC0.xy, v0.xx
texld r0, v0.xy, s0
mov oC0.zw, r0.ww
