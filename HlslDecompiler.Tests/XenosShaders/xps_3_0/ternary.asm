xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
dcl_texcoord0 r0
dcl_texcoord1 r1
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          sgt r2, r0, r1
/*    2   */          cndgt oC0, r2, r0, r1
