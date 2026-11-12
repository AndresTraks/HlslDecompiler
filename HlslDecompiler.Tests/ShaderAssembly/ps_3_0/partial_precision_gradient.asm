ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
dcl_2d s1
texld_pp r0, v0.xy, s0
dsx r0.zw, v0.xy
dsy r1.xy, v0.xy
texldd r0, r0.xy, s1, r0.zw, r1.xy
mul oC0, r0, c0
