hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 6
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[3][0]
dcl_output o0
dcl_output o1.x
dcl_temps 1
ult r0.x, vOutputControlPointID, l(3)
movc r0.x, r0.x, l(1), l(0.5)
udiv null, r0.y, vOutputControlPointID, l(3)
mul o0, r0.x, v[r0.y][0]
utof r0.x, vOutputControlPointID
add o1.x, r0.x, v[r0.y][0].w
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 3
dcl_input vForkInstanceID
dcl_input vocp[6][1].x
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 3
mov r0.x, vForkInstanceID.x
add r0.y, vocp[r0.x + 3][1].x, vocp[r0.x][1].x
mov o[r0.x].x, r0.y
ret
hs_join_phase
dcl_input vpc0.x
dcl_input vpc1.x
dcl_input vpc2.x
dcl_output_siv o3.x, finalTriInsideTessFactor
dcl_temps 1
add r0.x, vpc0.x, vpc1.x
add r0.x, r0.x, vpc2.x
mul o3.x, r0.x, l(0.333333343)
ret
