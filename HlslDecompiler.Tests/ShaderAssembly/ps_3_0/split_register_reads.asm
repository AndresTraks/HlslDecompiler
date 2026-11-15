ps_3_0
dcl_texcoord v0.xy
dcl_texcoord1 v1.xy
dcl_2d s0
texld r0, v0.xy, s0
mul oC0.xy, r0.xx, v0.xy
mul oC0.zw, r0.xx, v1.xy
