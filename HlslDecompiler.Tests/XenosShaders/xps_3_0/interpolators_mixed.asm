xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
dcl_texcoord1 r0.xy
dcl_texcoord3 r1.xyz
dcl_color0_centroid r2
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          mad oC0.xyz_, r2.xyzz, r0.xxxx, r1.xyzz
/*    2   */          mad oC0.___w, r2.wwww, r0.xxxx, r0.yyyy
